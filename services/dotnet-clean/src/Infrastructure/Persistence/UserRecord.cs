namespace Infrastructure.Persistence;

/// <summary>
/// users テーブルの行そのもの。**エンティティ（Domain.Entities.User）ではない。**
///
/// Hanami/ROM でいう Struct、Laravel でいう Model にあたる。
/// 「DB から読んだ行」と「業務上のユーザー」を同じ型にしないこと。
/// 同じにすると、カラムの都合（nullable・created_at 等）が全層へ伝播する。
///
/// このクラスを Infrastructure の外へ出さない。変換は Repository が行い、
/// その境界で永続化の都合を断ち切る。
/// </summary>
internal sealed class UserRecord
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // 既存スキーマでは nullable。Django が所有するカラムで、
    // このサービスは読み書きの対象にしていない。
    public string? Bio { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
