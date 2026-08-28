using System.Text.RegularExpressions;

namespace Domain.ValueObjects;

/// <summary>
/// メールアドレスの値オブジェクト。
///
/// 生成時に検証することで「不正な Email が存在しない」ことを型で保証する。
/// record struct にして不変性と値等価性を言語機能で得ている
/// （C# では Equals/GetHashCode を手書きする必要がない）。
///
/// コンストラクタを private にし、Create を通す以外の生成経路を塞いでいる。
/// </summary>
public readonly partial record struct Email
{
    private const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    /// <summary>検証済みの Email を返す。不正なら InvalidValueException。</summary>
    public static Email Create(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > MaxLength || !EmailPattern().IsMatch(trimmed))
        {
            throw new InvalidValueException("メールアドレスの形式が不正です");
        }

        return new Email(trimmed);
    }

    /// <summary>@ より前を返す。既定の表示名の導出に使う。</summary>
    public string LocalPart
    {
        get
        {
            var i = Value.IndexOf('@', StringComparison.Ordinal);
            return i >= 0 ? Value[..i] : Value;
        }
    }

    public override string ToString() => Value;

    // ソース生成の正規表現。実行時にパターンを解釈しないぶん速く、
    // パターンの誤りがコンパイル時に分かる。
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
