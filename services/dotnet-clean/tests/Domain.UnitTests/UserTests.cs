using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.UnitTests;

public sealed class UserTests
{
    private static readonly UserId Id = UserId.Create("11111111-1111-1111-1111-111111111111");
    private static readonly Email Mail = Email.Create("taro@example.com");

    [Fact]
    public void 新規登録すると表示名はメールアドレスから導出される()
    {
        var user = User.Register(Id, Mail);

        Assert.Equal("taro", user.DisplayName.Value);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void 有効なユーザーはサインインできる()
    {
        Assert.True(User.Register(Id, Mail).CanSignIn());
    }

    [Fact]
    public void 無効化するとサインインできない()
    {
        var user = User.Register(Id, Mail);
        user.Deactivate();

        Assert.False(user.CanSignIn());
    }

    [Fact]
    public void 等価性は識別子だけで決まる()
    {
        // エンティティは同一性を持つ。表示名が違っても ID が同じなら同じユーザー。
        // record にすると値等価性になり、この性質が壊れる。
        var a = User.Register(Id, Mail);
        var b = User.Restore(Id, Mail, DisplayName.Create("別の名前"), isActive: false);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void 識別子が違えば別のユーザー()
    {
        var other = UserId.Create("22222222-2222-2222-2222-222222222222");

        Assert.NotEqual(User.Register(Id, Mail), User.Register(other, Mail));
    }
}
