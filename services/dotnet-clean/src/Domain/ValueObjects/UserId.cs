namespace Domain.ValueObjects;

/// <summary>
/// ユーザー識別子の値オブジェクト。Cognito の sub をそのまま識別子として扱う。
///
/// 素の string を持ち回すと「どの ID なのか」が型から失われる。
/// UserId と Email をうっかり取り違えても、型が違えばコンパイルが止める。
/// </summary>
public readonly record struct UserId
{
    // users.id は varchar(64)。ここを超える値は永続化できないため生成時に弾く。
    private const int MaxLength = 64;

    private UserId(string value) => Value = value;

    public string Value { get; }

    public static UserId Create(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxLength)
        {
            throw new InvalidValueException("ユーザーIDが不正です");
        }

        return new UserId(trimmed);
    }

    public override string ToString() => Value;
}
