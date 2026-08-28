using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Abstractions;

/// <summary>
/// 認証基盤の契約。
///
/// **Cognito という具体名をここに出さないこと。**
/// Application は「認証できること」だけを知っていればよい。
/// </summary>
public interface IAuthenticator
{
    /// <summary>認証情報を検証しトークンを発行する。失敗時は AuthenticationFailedException。</summary>
    Task<AuthTokens> SignInAsync(Email email, string password, CancellationToken cancellationToken);

    /// <summary>アクセストークンを検証し本人情報を返す。失敗時は AuthenticationFailedException。</summary>
    Task<VerifiedIdentity> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken);
}
