<?php

declare(strict_types=1);

namespace App\Http\Responses;

use Illuminate\Http\JsonResponse;

/**
 * HTTP レスポンス生成。
 *
 * 形の統一をここに集約する。各 Controller が個別に response()->json() を
 * 組み立てると、エラー本文の形が少しずつズレていく。
 */
final class JsonApiResponse
{
    /** @param array<string, mixed> $payload */
    public static function ok(array $payload, int $status = 200): JsonResponse
    {
        return new JsonResponse($payload, $status);
    }

    public static function error(string $detail, int $status): JsonResponse
    {
        return new JsonResponse(['detail' => $detail], $status);
    }
}
