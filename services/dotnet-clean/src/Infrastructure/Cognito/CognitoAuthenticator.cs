using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Application.Abstractions;
using Domain;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Cognito;

/// <summary>
/// IAuthenticator の Cognito 実装。
///
/// 契約(interface)は Application 側で定義され、この型がそれを満たす（依存性逆転）。
/// </summary>
internal sealed class CognitoAuthenticator : IAuthenticator
{
    // 実 Cognito では ExpiresIn が必ず返るが、cognito-local では 0 のことがある。
    // ここで落とすと「本番でだけ動く」実装になるため既定値へフォールバックする。
    private const int DefaultExpiresInSeconds = 3600;

    // JWKS を都度取りに行くとレート制限に当たり、レイテンシも増える。
    // Lambda では実行環境が再利用されるため、キャッシュが効く。
    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(12);

    private readonly IAmazonCognitoIdentityProvider _client;
    private readonly CognitoOptions _options;
    private readonly ILogger<CognitoAuthenticator> _logger;
    private readonly HttpClient _http;

    private readonly SemaphoreSlim _jwksLock = new(1, 1);
    private IList<SecurityKey>? _cachedKeys;
    private DateTimeOffset _cachedAt;

    private readonly JwtSecurityTokenHandler _handler = new();

    public CognitoAuthenticator(
        IAmazonCognitoIdentityProvider client,
        IOptions<CognitoOptions> options,
        ILogger<CognitoAuthenticator> logger,
        HttpClient http)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
        _http = http;
    }

    public async Task<AuthTokens> SignInAsync(
        Email email,
        string password,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["USERNAME"] = email.Value,
            ["PASSWORD"] = password,
        };

        if (!string.IsNullOrEmpty(_options.ClientSecret))
        {
            parameters["SECRET_HASH"] = SecretHash(email.Value);
        }

        InitiateAuthResponse response;
        try
        {
            response = await _client.InitiateAuthAsync(
                new InitiateAuthRequest
                {
                    ClientId = _options.ClientId,
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                    AuthParameters = parameters,
                },
                cancellationToken);
        }
        catch (AmazonCognitoIdentityProviderException ex) when (IsAuthFailure(ex))
        {
            // 「ユーザーが存在しない」と「パスワードが違う」を区別して返さないこと。
            // 区別するとアカウント列挙に使われる。
            // **ただしログには理由を残す。** 両方伏せると本番で追えない。
            _logger.LogInformation(
                "サインインに失敗しました（エラーコード: {ErrorCode}）", ex.ErrorCode);
            throw new AuthenticationFailedException();
        }

        if (response.AuthenticationResult is null)
        {
            // MFA 等で追加ステップが要求された場合
            _logger.LogWarning(
                "追加の認証ステップが要求されました: {Challenge}", response.ChallengeName);
            throw new AuthenticationFailedException();
        }

        var result = response.AuthenticationResult;
        return new AuthTokens(
            result.AccessToken ?? string.Empty,
            result.IdToken ?? string.Empty,
            result.RefreshToken ?? string.Empty,
            // cognito-local は ExpiresIn を返さないことがある
            result.ExpiresIn is > 0 ? result.ExpiresIn.Value : DefaultExpiresInSeconds);
    }

    public async Task<VerifiedIdentity> VerifyAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var signingKeys = await GetSigningKeysAsync(cancellationToken);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // cognito-local は localhost:9229 を iss に刻むため、
            // 到達可能な JWKS URL とは別の値をここで期待する。
            ValidIssuer = _options.ResolvedIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys,
            // Cognito のアクセストークンには aud が無い（代わりに client_id が入る）。
            // ValidateAudience を true のままにすると必ず失敗する。下で明示的に照合する。
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        JwtSecurityToken token;
        try
        {
            _handler.ValidateToken(accessToken, parameters, out var validated);
            token = (JwtSecurityToken)validated;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            _logger.LogInformation("アクセストークンの検証に失敗しました: {Reason}", ex.Message);
            throw new AuthenticationFailedException();
        }

        // aud の代わりに client_id を照合する。これを省くと、
        // 同じユーザープールの別クライアントのトークンを受け入れてしまう。
        if (GetClaim(token, "client_id") != _options.ClientId)
        {
            throw new AuthenticationFailedException();
        }

        // ID トークンをアクセストークンとして使い回されるのを防ぐ。
        if (GetClaim(token, "token_use") != "access")
        {
            throw new AuthenticationFailedException();
        }

        var subject = GetClaim(token, "sub");
        if (string.IsNullOrEmpty(subject))
        {
            throw new AuthenticationFailedException();
        }

        // アクセストークンに email は含まれないことがある
        return new VerifiedIdentity(subject, GetClaim(token, "email"));
    }

    /// <summary>
    /// 署名鍵(JWKS)を取得する。取得済みで期限内ならキャッシュを返す。
    ///
    /// **ConfigurationManager を使っていない。** あれは OIDC の
    /// ディスカバリ文書(.well-known/openid-configuration)を前提にするが、
    /// cognito-local が公開するのは JWKS そのものなので噛み合わない。
    /// 素直に JWKS を取って JsonWebKeySet で解釈する。
    /// </summary>
    private async Task<IList<SecurityKey>> GetSigningKeysAsync(
        CancellationToken cancellationToken)
    {
        if (_cachedKeys is not null && DateTimeOffset.UtcNow - _cachedAt < JwksCacheDuration)
        {
            return _cachedKeys;
        }

        await _jwksLock.WaitAsync(cancellationToken);
        try
        {
            // ロック待ちの間に他のリクエストが取得済みかもしれない
            if (_cachedKeys is not null && DateTimeOffset.UtcNow - _cachedAt < JwksCacheDuration)
            {
                return _cachedKeys;
            }

            var json = await _http.GetStringAsync(_options.ResolvedJwksUrl, cancellationToken);
            var keys = JsonWebKeySet.Create(json).GetSigningKeys();

            _cachedKeys = keys;
            _cachedAt = DateTimeOffset.UtcNow;
            return keys;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // JWKS が引けないのは認証の失敗ではなく障害。500 として扱わせる。
            _logger.LogError(ex, "JWKS の取得に失敗しました: {Url}", _options.ResolvedJwksUrl);
            throw;
        }
        finally
        {
            _jwksLock.Release();
        }
    }

    private static string? GetClaim(JwtSecurityToken token, string type)
        => token.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private string SecretHash(string username)
    {
        var bytes = Encoding.UTF8.GetBytes(username + _options.ClientId);
        var key = Encoding.UTF8.GetBytes(_options.ClientSecret);
        return Convert.ToBase64String(HMACSHA256.HashData(key, bytes));
    }

    /// <summary>
    /// 「認証情報が正しくない」系のエラーかを判定する。
    ///
    /// **例外の型ではなく、エラーコード文字列で判定している。**
    /// 実 Cognito は NotAuthorizedException 等を型付きで返すが、
    /// cognito-local や一部の経路では汎用の例外として返るため、
    /// 型で見ると取りこぼして 500 になる（他スタックで実際に踏んだ）。
    /// ErrorCode は AWS SDK が必ず公開する。
    /// </summary>
    private static bool IsAuthFailure(AmazonCognitoIdentityProviderException ex)
        => ex.ErrorCode is not null && AuthFailureCodes.Contains(ex.ErrorCode);

    private static readonly HashSet<string> AuthFailureCodes =
    [
        "NotAuthorizedException",
        "UserNotFoundException",
        "InvalidPasswordException",
        "InvalidParameterException",
        "UserNotConfirmedException",
    ];
}
