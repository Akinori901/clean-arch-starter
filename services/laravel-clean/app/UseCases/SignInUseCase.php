<?php

declare(strict_types=1);

namespace App\UseCases;

use App\DataTransferObjects\SignInResultDto;
use App\Services\AuthService;

/**
 * サインインユースケース。
 *
 * Service のオーケストレーションとトランザクション境界を担う。
 * 公開メソッドは execute() のみ。
 */
final readonly class SignInUseCase
{
    public function __construct(
        private AuthService $auth,
    ) {
    }

    public function execute(string $email, string $password): SignInResultDto
    {
        // 1. 認証基盤で認証する
        $tokens = $this->auth->authenticate($email, $password);

        // 2. 本人を特定し、ローカル側のユーザーを解決する（初回なら作成）
        $user = $this->auth->resolveUser($tokens->accessToken);

        // 3. 業務上の可否を検証する（Cognito が通しても無効化なら拒否）
        $this->auth->assertCanSignIn($user);

        return new SignInResultDto(tokens: $tokens, user: $user);
    }
}
