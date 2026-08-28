namespace Infrastructure.Cognito;

/// <summary>
/// Cognito 接続の設定。
///
/// IssuerOverride / JwksUrlOverride はローカルのエミュレータ向け。
/// **本番では両方とも空にすること**（実 Cognito の値が自動で組み立てられる）。
/// </summary>
public sealed class CognitoOptions
{
    public string UserPoolId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>SPA ではシークレット無し。サーバ間認証で使う場合のみ設定する。</summary>
    public string ClientSecret { get; set; } = string.Empty;

    public string Region { get; set; } = "ap-northeast-1";

    /// <summary>ローカルのエミュレータのエンドポイント。本番では空。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// トークンの iss として期待する値。
    ///
    /// cognito-local は自分の公開 URL(localhost:9229) を iss に刻む一方、
    /// コンテナからは cognito:9229 でしか到達できない。
    /// そのため「検証に使う issuer」と「JWKS の取得先」を分けて指定する。
    /// </summary>
    public string IssuerOverride { get; set; } = string.Empty;

    /// <summary>JWKS の取得先 URL。上記の理由で issuer とは別に指定できるようにしてある。</summary>
    public string JwksUrlOverride { get; set; } = string.Empty;

    /// <summary>実際に使う issuer。override が無ければ実 Cognito の形式を組み立てる。</summary>
    public string ResolvedIssuer => string.IsNullOrEmpty(IssuerOverride)
        ? $"https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}"
        : IssuerOverride;

    /// <summary>実際に使う JWKS URL。</summary>
    public string ResolvedJwksUrl => string.IsNullOrEmpty(JwksUrlOverride)
        ? $"{ResolvedIssuer}/.well-known/jwks.json"
        : JwksUrlOverride;
}
