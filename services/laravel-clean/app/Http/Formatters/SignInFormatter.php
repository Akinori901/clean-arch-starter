<?php

declare(strict_types=1);

namespace App\Http\Formatters;

use App\DataTransferObjects\SignInResultDto;

/**
 * Formatter は配列/文字列を返す。
 *
 * **JsonResponse を返さないこと。** HTTP レスポンスの生成は Response 層の責務。
 * ここで分けておくと、同じ整形を CLI 出力やメール本文へ再利用できる。
 */
final class SignInFormatter
{
    /** @return array<string, mixed> */
    public static function format(SignInResultDto $result): array
    {
        return [
            'access_token' => $result->tokens->accessToken,
            'id_token' => $result->tokens->idToken,
            'refresh_token' => $result->tokens->refreshToken,
            'expires_in' => $result->tokens->expiresIn,
            'user' => [
                'id' => $result->user->id,
                'email' => $result->user->email,
                'display_name' => $result->user->displayName,
            ],
        ];
    }
}
