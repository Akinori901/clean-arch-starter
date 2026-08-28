using Domain;
using Domain.ValueObjects;

namespace Domain.UnitTests;

/// <summary>
/// 値オブジェクトの検証。
///
/// **DB も AWS も Web ホストも起動しない。** Domain.csproj が
/// 何も参照していないので、そもそも起動しようがない。
/// これが層を分けた見返り。
/// </summary>
public sealed class EmailTests
{
    [Theory]
    [InlineData("taro@example.com")]
    [InlineData("a.b+c@sub.example.co.jp")]
    public void 正しい形式なら生成できる(string raw)
    {
        Assert.Equal(raw, Email.Create(raw).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-mark")]
    [InlineData("no@domain")]
    [InlineData("two@@example.com")]
    [InlineData(null)]
    public void 不正な形式なら生成できない(string? raw)
    {
        Assert.Throws<InvalidValueException>(() => Email.Create(raw));
    }

    [Fact]
    public void 前後の空白は除去される()
    {
        Assert.Equal("taro@example.com", Email.Create("  taro@example.com  ").Value);
    }

    [Fact]
    public void LocalPartはアットマークより前を返す()
    {
        Assert.Equal("taro", Email.Create("taro@example.com").LocalPart);
    }

    [Fact]
    public void 等価性は値で決まる()
    {
        Assert.Equal(Email.Create("taro@example.com"), Email.Create("taro@example.com"));
    }
}

public sealed class UserIdTests
{
    [Fact]
    public void 空文字は生成できない()
    {
        Assert.Throws<InvalidValueException>(() => UserId.Create("  "));
    }

    [Fact]
    public void 六十四文字を超えると生成できない()
    {
        // users.id は varchar(64)。永続化できない値を作らせない。
        Assert.Throws<InvalidValueException>(() => UserId.Create(new string('a', 65)));
    }

    [Fact]
    public void 六十四文字なら生成できる()
    {
        Assert.Equal(64, UserId.Create(new string('a', 64)).Value.Length);
    }
}

public sealed class DisplayNameTests
{
    [Fact]
    public void 空文字は生成できない()
    {
        Assert.Throws<InvalidValueException>(() => DisplayName.Create(""));
    }

    [Fact]
    public void 五十文字なら生成できる()
    {
        Assert.Equal(50, DisplayName.Create(new string('あ', 50)).Value.Length);
    }

    [Fact]
    public void 五十文字を超えると生成できない()
    {
        Assert.Throws<InvalidValueException>(() => DisplayName.Create(new string('あ', 51)));
    }

    [Fact]
    public void 絵文字は一文字として数える()
    {
        // string.Length は UTF-16 のコード単位なので、サロゲートペアの絵文字を
        // 2 と数えてしまう。利用者から見た文字数と一致させる。
        var fiftyEmoji = string.Concat(Enumerable.Repeat("😀", 50));
        Assert.Equal(fiftyEmoji, DisplayName.Create(fiftyEmoji).Value);
    }
}
