using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// ユーザーのエンティティ。同一性を持つ（値が変わっても ID が同じなら同じ User）。
///
/// **record ではなく class にしている。** record は値等価性を既定にするが、
/// エンティティの等価性は識別子だけで決まる。record にすると
/// 「表示名を変えたら別人」という誤った等価性になる。
/// </summary>
public sealed class User
{
    private User(UserId id, Email email, DisplayName displayName, bool isActive)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        IsActive = isActive;
    }

    public UserId Id { get; }

    public Email Email { get; private set; }

    public DisplayName DisplayName { get; private set; }

    // 外から書き換えられないよう private set。状態変更はメソッド経由に限る。
    public bool IsActive { get; private set; }

    /// <summary>新規ユーザーを組み立てる。表示名はメールアドレスから導出する。</summary>
    public static User Register(UserId id, Email email)
        => new(id, email, DisplayName.Create(email.LocalPart), isActive: true);

    /// <summary>永続化済みのデータからエンティティを復元する（Infrastructure が使う）。</summary>
    public static User Restore(UserId id, Email email, DisplayName displayName, bool isActive)
        => new(id, email, displayName, isActive);

    /// <summary>
    /// サインイン可能かを判定する（ビジネスルール）。
    ///
    /// この判定を Application や Web の if で書かないこと。
    /// ルールをエンティティに置かないと、同じ判定が各所へ散らばる。
    /// </summary>
    public bool CanSignIn() => IsActive;

    /// <summary>アカウントを無効化する。</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>表示名を変更する。</summary>
    public void Rename(DisplayName displayName) => DisplayName = displayName;

    // エンティティの等価性は識別子のみで決まる。
    public override bool Equals(object? obj) => obj is User other && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();
}
