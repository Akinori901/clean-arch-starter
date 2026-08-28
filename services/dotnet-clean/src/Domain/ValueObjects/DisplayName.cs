namespace Domain.ValueObjects;

/// <summary>
/// 表示名の値オブジェクト。
/// </summary>
public readonly record struct DisplayName
{
    // 文字数は「文字」で数える。string.Length は UTF-16 のコード単位なので、
    // 絵文字（サロゲートペア）を 2 と数えてしまい、利用者から見た文字数とずれる。
    private const int MaxLength = 50;

    private DisplayName(string value) => Value = value;

    public string Value { get; }

    public static DisplayName Create(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || CountTextElements(trimmed) > MaxLength)
        {
            throw new InvalidValueException("表示名が不正です");
        }

        return new DisplayName(trimmed);
    }

    private static int CountTextElements(string value)
    {
        var count = 0;
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    public override string ToString() => Value;
}
