using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// EF Core の DbContext。
///
/// **マイグレーションは作らないこと。**
/// users テーブルは Django が所有し、既存 4 スタックと共有している。
/// EF Core からもマイグレーションを生成すると、同じテーブルを 2 つの
/// マイグレーション履歴が管理することになり、必ず壊れる。
/// ここでは「既存テーブルへマップするだけ」に徹する。
/// </summary>
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserRecord> Users => Set<UserRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<UserRecord>();

        user.ToTable("users");
        user.HasKey(u => u.Id);

        // カラム名は既存スキーマ（snake_case）に明示的に合わせる。
        // EF Core の既定は PascalCase のままなので、指定しないと列が見つからない。
        user.Property(u => u.Id).HasColumnName("id").HasMaxLength(64);
        user.Property(u => u.Email).HasColumnName("email").HasMaxLength(254);
        user.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        user.Property(u => u.Bio).HasColumnName("bio");
        user.Property(u => u.IsActive).HasColumnName("is_active");

        // created_at / updated_at は DB 側の DEFAULT で入る（Django が db_default を付けている）。
        // ValueGeneratedOnAdd を指定して、EF Core が INSERT 文から外すようにする。
        // 指定しないと既定値の 0001-01-01 を送ってしまい、MySQL が範囲外で拒否する。
        user.Property(u => u.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        user.Property(u => u.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd();

        user.HasIndex(u => u.Email).IsUnique();
    }
}
