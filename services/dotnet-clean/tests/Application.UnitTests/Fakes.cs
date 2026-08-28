using Application.Abstractions;
using Domain;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UnitTests;

/// <summary>
/// 契約(interface)を満たすテスト用の実装。
///
/// **モックライブラリを使っていない。** この規模なら手書きの Fake で足り、
/// 「何が起きるか」がテストを読むだけで分かる。
/// Application が契約だけに依存しているからこそ、
/// DB も Cognito も起動せずに差し替えられる。
/// </summary>
internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<string, User> _users = [];

    public int SaveCount { get; private set; }

    public void Seed(User user) => _users[user.Id.Value] = user;

    public Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken)
        => Task.FromResult(_users.GetValueOrDefault(id.Value));

    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken)
        => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));

    public Task<User> SaveAsync(User user, CancellationToken cancellationToken)
    {
        SaveCount++;
        _users[user.Id.Value] = user;
        return Task.FromResult(user);
    }
}

internal sealed class FakeAuthenticator : IAuthenticator
{
    public const string Subject = "11111111-1111-1111-1111-111111111111";

    public bool ShouldFailSignIn { get; set; }

    public bool ShouldFailVerify { get; set; }

    public Task<AuthTokens> SignInAsync(Email email, string password, CancellationToken cancellationToken)
        => ShouldFailSignIn
            ? throw new AuthenticationFailedException()
            : Task.FromResult(new AuthTokens("access", "id", "refresh", 3600));

    public Task<VerifiedIdentity> VerifyAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
        => ShouldFailVerify
            ? throw new AuthenticationFailedException()
            : Task.FromResult(new VerifiedIdentity(Subject, "taro@example.com"));
}

internal sealed class FakeHealthProbe(string name, Exception? failure = null) : IHealthProbe
{
    public string Name => name;

    public Task CheckAsync(CancellationToken cancellationToken)
        => failure is null ? Task.CompletedTask : Task.FromException(failure);
}
