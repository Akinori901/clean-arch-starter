<?php

declare(strict_types=1);

namespace App\Services;

use App\DataTransferObjects\AuthTokensDto;
use App\DataTransferObjects\UserDto;
use App\Exceptions\AuthenticationFailedException;
use App\Helpers\DisplayNameHelper;
use App\Repositories\AuthRepositoryInterface;
use App\Repositories\UserRepositoryInterface;

/**
 * 認証まわりのビジネスロジック。
 *
 * **他の Service を呼ばないこと。** 複数 Service の調整は UseCase の仕事。
 * 依存するのは RepositoryInterface / Helper / Dto / Exception / Enum のみ。
 */
final readonly class AuthService
{
    public function __construct(
        private AuthRepositoryInterface $auth,
        private UserRepositoryInterface $users,
    ) {
    }

    public function authenticate(string $email, string $password): AuthTokensDto
    {
        return $this->auth->signIn($email, $password);
    }

    /**
     * トークンから本人を特定し、ローカル側のユーザーを解決する。
     *
     * Cognito が正。ローカルはプロフィール保持のみを担うため、
     * 初回サインイン時はここで作る。
     */
    public function resolveUser(string $accessToken): UserDto
    {
        $identity = $this->auth->verifyAccessToken($accessToken);

        $user = $this->users->findById($identity->subject);
        if ($user === null) {
            $email = $identity->email;
            $user = $this->users->save(new UserDto(
                id: $identity->subject,
                email: $email,
                displayName: DisplayNameHelper::fromEmail($email),
                isActive: true,
            ));
        }

        return $user;
    }

    /**
     * 業務上サインインを許可してよいかを検証する。
     *
     * @throws AuthenticationFailedException
     */
    public function assertCanSignIn(UserDto $user): void
    {
        if (! $user->canSignIn()) {
            throw new AuthenticationFailedException('このアカウントは無効化されています');
        }
    }
}
