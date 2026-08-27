<?php

declare(strict_types=1);

namespace Tests\Unit;

use App\DataTransferObjects\AuthTokensDto;
use App\DataTransferObjects\UserDto;
use App\DataTransferObjects\VerifiedIdentityDto;
use App\Exceptions\AuthenticationFailedException;
use App\Repositories\AuthRepositoryInterface;
use App\Repositories\UserRepositoryInterface;
use App\Services\AuthService;
use App\UseCases\SignInUseCase;
use PHPUnit\Framework\TestCase;

/**
 * 契約が interface になっていることの実利。
 * Cognito も MySQL も無しでユースケースを完全に検証できる。
 */
final class SignInUseCaseTest extends TestCase
{
    public function test_first_sign_in_provisions_local_user(): void
    {
        $users = $this->inMemoryUsers();
        $useCase = new SignInUseCase(new AuthService($this->fakeAuth(), $users));

        $result = $useCase->execute('taro@example.com', 'correct-pass');

        $this->assertSame('sub-1', $result->user->id);
        $this->assertSame('taro', $result->user->displayName);
        $this->assertNotNull($users->findById('sub-1'));
    }

    public function test_wrong_password_is_rejected(): void
    {
        $useCase = new SignInUseCase(
            new AuthService($this->fakeAuth(), $this->inMemoryUsers()),
        );

        $this->expectException(AuthenticationFailedException::class);
        $useCase->execute('taro@example.com', 'wrong-pass');
    }

    public function test_deactivated_user_is_rejected_even_if_cognito_accepts(): void
    {
        $users = $this->inMemoryUsers([
            new UserDto('sub-1', 'taro@example.com', 'taro', isActive: false),
        ]);
        $useCase = new SignInUseCase(new AuthService($this->fakeAuth(), $users));

        $this->expectException(AuthenticationFailedException::class);
        $this->expectExceptionMessageMatches('/無効化/');
        $useCase->execute('taro@example.com', 'correct-pass');
    }

    private function fakeAuth(): AuthRepositoryInterface
    {
        return new class implements AuthRepositoryInterface
        {
            public function signIn(string $email, string $password): AuthTokensDto
            {
                if ($password !== 'correct-pass') {
                    throw new AuthenticationFailedException(
                        'メールアドレスまたはパスワードが正しくありません',
                    );
                }

                return new AuthTokensDto('access-sub-1', 'id', 'refresh', 3600);
            }

            public function verifyAccessToken(string $token): VerifiedIdentityDto
            {
                if (! str_starts_with($token, 'access-')) {
                    throw new AuthenticationFailedException('トークンが無効です');
                }

                return new VerifiedIdentityDto(
                    substr($token, strlen('access-')),
                    'taro@example.com',
                );
            }
        };
    }

    /** @param list<UserDto> $seed */
    private function inMemoryUsers(array $seed = []): UserRepositoryInterface
    {
        return new class($seed) implements UserRepositoryInterface
        {
            /** @var array<string, UserDto> */
            private array $store = [];

            /** @param list<UserDto> $seed */
            public function __construct(array $seed)
            {
                foreach ($seed as $u) {
                    $this->store[$u->id] = $u;
                }
            }

            public function findById(string $id): ?UserDto
            {
                return $this->store[$id] ?? null;
            }

            public function findByEmail(string $email): ?UserDto
            {
                foreach ($this->store as $u) {
                    if ($u->email === $email) {
                        return $u;
                    }
                }

                return null;
            }

            public function save(UserDto $user): UserDto
            {
                return $this->store[$user->id] = $user;
            }
        };
    }
}
