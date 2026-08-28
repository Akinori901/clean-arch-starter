namespace Domain;

/// <summary>
/// ドメイン例外。
///
/// **HTTP ステータスコードをここに持ち込まないこと。**
/// 「認証に失敗した」はドメインの語彙だが、「401」は Web 層の語彙である。
/// 変換は Web 層だけが行う（NetArchTest が StatusCode 等の混入を検知する）。
/// </summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>値オブジェクトの生成に失敗した（入力が不正）。</summary>
public sealed class InvalidValueException(string message) : DomainException(message);

/// <summary>
/// 認証に失敗した。
///
/// **メッセージを「ユーザーが存在しない」「パスワードが違う」で
/// 出し分けないこと。** 区別するとアカウント列挙に使われる。
/// 既存 4 スタックすべてで同一のメッセージに揃えてある。
/// </summary>
public sealed class AuthenticationFailedException()
    : DomainException("メールアドレスまたはパスワードが正しくありません");

/// <summary>ユーザーが見つからない。</summary>
public sealed class UserNotFoundException() : DomainException("ユーザーが見つかりません");

/// <summary>アカウントが無効化されている。</summary>
public sealed class UserDeactivatedException() : DomainException("このアカウントは無効化されています");
