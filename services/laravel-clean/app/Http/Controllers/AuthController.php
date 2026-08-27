<?php

declare(strict_types=1);

namespace App\Http\Controllers;

use App\Exceptions\AuthenticationFailedException;
use App\Http\Formatters\SignInFormatter;
use App\Http\Formatters\UserFormatter;
use App\Http\Requests\SignInRequest;
use App\Http\Responses\JsonApiResponse;
use App\UseCases\GetCurrentUserUseCase;
use App\UseCases\SignInUseCase;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;

/**
 * Controller がやってよいのは 3 つだけ:
 *   1. UseCase へ渡す
 *   2. 例外を catch する
 *   3. レスポンスの出し分け
 *
 * ビジネスロジックを書かないこと。
 */
final class AuthController
{
    public function signIn(SignInRequest $request, SignInUseCase $useCase): JsonResponse
    {
        try {
            $result = $useCase->execute(
                $request->string('email')->toString(),
                $request->string('password')->toString(),
            );
        } catch (AuthenticationFailedException $e) {
            // 業務の語彙（認証失敗）を HTTP の語彙（401）へ翻訳するのはここ
            return JsonApiResponse::error($e->getMessage(), 401);
        }

        return JsonApiResponse::ok(SignInFormatter::format($result));
    }

    public function me(Request $request, GetCurrentUserUseCase $useCase): JsonResponse
    {
        $token = $this->bearerToken($request);
        if ($token === null) {
            return JsonApiResponse::error('Authorization ヘッダがありません', 401);
        }

        try {
            $user = $useCase->execute($token);
        } catch (AuthenticationFailedException $e) {
            return JsonApiResponse::error($e->getMessage(), 401);
        }

        return JsonApiResponse::ok(UserFormatter::format($user));
    }

    private function bearerToken(Request $request): ?string
    {
        $header = $request->header('Authorization', '');
        if (! is_string($header) || ! str_starts_with($header, 'Bearer ')) {
            return null;
        }

        return trim(substr($header, 7)) ?: null;
    }
}
