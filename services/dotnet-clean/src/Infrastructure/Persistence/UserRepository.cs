using Application.Abstractions;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// IUserRepository の EF Core 実装。
///
/// **DbContext / DbSet を触ってよいのはこの層だけ。**
/// 返す直前に必ず UserRecord → Domain.Entities.User へ変換する。
/// </summary>
internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken)
    {
        // 読み取り専用なので AsNoTracking。変更追跡のコストを払う理由がない。
        var record = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id.Value, cancellationToken);

        return record is null ? null : ToEntity(record);
    }

    public async Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken)
    {
        var record = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, cancellationToken);

        return record is null ? null : ToEntity(record);
    }

    public async Task<User> SaveAsync(User user, CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id.Value, cancellationToken);

        if (existing is null)
        {
            db.Users.Add(new UserRecord
            {
                Id = user.Id.Value,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                IsActive = user.IsActive,
            });
        }
        else
        {
            existing.Email = user.Email.Value;
            existing.DisplayName = user.DisplayName.Value;
            existing.IsActive = user.IsActive;
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>DB の行を Domain のエンティティへ変換する。この境界で永続化の都合を断ち切る。</summary>
    private static User ToEntity(UserRecord record)
        => User.Restore(
            UserId.Create(record.Id),
            Email.Create(record.Email),
            DisplayName.Create(record.DisplayName),
            record.IsActive);
}
