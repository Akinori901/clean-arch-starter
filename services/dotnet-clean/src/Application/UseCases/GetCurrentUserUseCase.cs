using Application.Abstractions;
using Domain;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.UseCases;

/// <summary>アクセストークンから現在のユーザーを返すユースケース。</summary>
public sealed class GetCurrentUserUseCase(IAuthenticator authenticator, IUserRepository users)
{
    public async Task<User> ExecuteAsync(string accessToken, CancellationToken cancellationToken)
    {
        var identity = await authenticator.VerifyAccessTokenAsync(accessToken, cancellationToken);
        var id = UserId.Create(identity.Subject);

        // トークンは正しいがローカルに行が無い状態。
        // 「見つからない」を null で受けて、ここでドメイン例外へ翻訳する。
        return await users.FindByIdAsync(id, cancellationToken)
               ?? throw new UserNotFoundException();
    }
}
