using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Abstractions;

/// <summary>
/// ユーザーの永続化契約。
///
/// **契約は「使う側」である Application に置き、Infrastructure がそれを満たす。**
/// これが依存性逆転の書き方で、C# では Infrastructure.csproj が
/// Application.csproj を参照する形（内向きの矢印）として現れる。
///
/// 戻り値は必ず Domain の型。EF Core のエンティティや DbSet を返さないこと。
/// 返した瞬間に、永続化の都合が Application より上へ漏れ出す。
/// </summary>
public interface IUserRepository
{
    /// <summary>ID で引く。見つからなければ null（例外にしない＝呼び出し側が分岐できる）。</summary>
    Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>メールアドレスで引く。見つからなければ null。</summary>
    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>新規・更新の両方を担う（upsert）。</summary>
    Task<User> SaveAsync(User user, CancellationToken cancellationToken);
}
