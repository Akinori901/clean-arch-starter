using Application.UseCases;
using Domain;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UnitTests;

public sealed class SignInUseCaseTests
{
    private readonly FakeAuthenticator _auth = new();
    private readonly FakeUserRepository _users = new();

    private SignInUseCase UseCase => new(_auth, _users);

    [Fact]
    public async Task 正しい認証情報ならトークンとユーザーが返る()
    {
        var result = await UseCase.ExecuteAsync(
            "taro@example.com", "Passw0rd!", TestContext.Current.CancellationToken);

        Assert.Equal("access", result.Tokens.AccessToken);
        Assert.Equal(FakeAuthenticator.Subject, result.User.Id.Value);
    }

    [Fact]
    public async Task 初回サインインならローカルにユーザーが作られる()
    {
        // Cognito が正で、ローカルはプロフィールの保持のみを担う。
        await UseCase.ExecuteAsync(
            "taro@example.com", "Passw0rd!", TestContext.Current.CancellationToken);

        Assert.Equal(1, _users.SaveCount);
    }

    [Fact]
    public async Task 二回目以降は既存ユーザーを使い保存しない()
    {
        _users.Seed(User.Register(
            UserId.Create(FakeAuthenticator.Subject),
            Email.Create("taro@example.com")));

        await UseCase.ExecuteAsync(
            "taro@example.com", "Passw0rd!", TestContext.Current.CancellationToken);

        Assert.Equal(0, _users.SaveCount);
    }

    [Fact]
    public async Task 認証に失敗すると認証エラーになる()
    {
        _auth.ShouldFailSignIn = true;

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => UseCase.ExecuteAsync("taro@example.com", "wrong-password", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task 無効化されたアカウントは認証基盤が通しても拒否する()
    {
        // Cognito 側は有効でも、ローカルで無効化していれば通さない。
        // 判定規則は Domain（User.CanSignIn）が持ち、ここでは呼ぶだけ。
        var user = User.Register(
            UserId.Create(FakeAuthenticator.Subject),
            Email.Create("taro@example.com"));
        user.Deactivate();
        _users.Seed(user);

        await Assert.ThrowsAsync<UserDeactivatedException>(
            () => UseCase.ExecuteAsync("taro@example.com", "Passw0rd!", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task メールアドレスの形式が不正なら認証基盤を呼ばない()
    {
        // 値オブジェクトの生成時点で弾かれるので、無駄な外部呼び出しが起きない。
        await Assert.ThrowsAsync<InvalidValueException>(
            () => UseCase.ExecuteAsync("not-an-email", "Passw0rd!", TestContext.Current.CancellationToken));
    }
}

public sealed class GetCurrentUserUseCaseTests
{
    private readonly FakeAuthenticator _auth = new();
    private readonly FakeUserRepository _users = new();

    private GetCurrentUserUseCase UseCase => new(_auth, _users);

    [Fact]
    public async Task 有効なトークンならユーザーが返る()
    {
        _users.Seed(User.Register(
            UserId.Create(FakeAuthenticator.Subject),
            Email.Create("taro@example.com")));

        var user = await UseCase.ExecuteAsync("access", TestContext.Current.CancellationToken);

        Assert.Equal("taro@example.com", user.Email.Value);
    }

    [Fact]
    public async Task トークンが不正なら認証エラーになる()
    {
        _auth.ShouldFailVerify = true;

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => UseCase.ExecuteAsync("broken", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task トークンは正しいがローカルに行が無ければ未検出になる()
    {
        await Assert.ThrowsAsync<UserNotFoundException>(
            () => UseCase.ExecuteAsync("access", TestContext.Current.CancellationToken));
    }
}
