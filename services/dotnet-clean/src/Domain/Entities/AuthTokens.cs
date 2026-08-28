namespace Domain.Entities;

/// <summary>
/// 認証成功時に発行されるトークン群。
///
/// 「認証したらトークンが得られる」はドメインの概念なので Domain に置く。
/// Application 側に置くと、Infrastructure が Application の型を必要とする点は
/// 変わらないが（そこは契約の実装なので正しい）、Domain に置くほうが
/// 「どの層からも参照してよい末端」として素直になる。
/// </summary>
/// <param name="AccessToken">API 認可に使うトークン。</param>
/// <param name="IdToken">本人属性を含むトークン。</param>
/// <param name="RefreshToken">再発行用トークン。</param>
/// <param name="ExpiresIn">アクセストークンの有効秒数。</param>
public sealed record AuthTokens(
    string AccessToken,
    string IdToken,
    string RefreshToken,
    int ExpiresIn);

/// <summary>検証済みトークンから取り出した本人情報。</summary>
/// <param name="Subject">Cognito の sub。</param>
/// <param name="Email">トークンに含まれていれば入る（アクセストークンには無いことがある）。</param>
public sealed record VerifiedIdentity(string Subject, string? Email);
