using Application.Abstractions;
using Application.Dto;
using Domain;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UseCases;

/// <summary>
/// サインインのユースケース。
///
/// 依存はすべてインターフェースで受け取り、コンストラクタで注入する。
/// ここで具象型（Cognito クライアント等）を使わないこと。
/// そもそも Application.csproj が AWS SDK を参照していないため書けない。
/// </summary>
public sealed class SignInUseCase(IAuthenticator authenticator, IUserRepository users)
{
    /// <summary>認証し、ローカル側のユーザーを解決して返す。</summary>
    public async Task<SignInResult> ExecuteAsync(
        string rawEmail,
        string password,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(rawEmail);

        // 1. 認証基盤（Cognito）で認証する
        var tokens = await authenticator.SignInAsync(email, password, cancellationToken);

        // 2. 検証済みトークンから本人を特定する
        var identity = await authenticator.VerifyAccessTokenAsync(tokens.AccessToken, cancellationToken);

        // 3. ローカル側のユーザーを解決する（初回サインインなら作る）
        //    Cognito が正で、ローカルはプロフィールの保持のみを担う。
        var user = await ResolveUserAsync(identity.Subject, email, cancellationToken);

        // 4. 無効化されたアカウントは、Cognito 側が通しても拒否する。
        //    判定規則は Domain が持つ。ここでは呼ぶだけ。
        if (!user.CanSignIn())
        {
            throw new UserDeactivatedException();
        }

        return new SignInResult(tokens, user);
    }

    private async Task<User> ResolveUserAsync(
        string subject,
        Email email,
        CancellationToken cancellationToken)
    {
        var id = UserId.Create(subject);

        var existing = await users.FindByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await users.SaveAsync(User.Register(id, email), cancellationToken);
    }
}
