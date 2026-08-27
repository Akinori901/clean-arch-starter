<?php

declare(strict_types=1);

namespace App\Http\Controllers;

use App\Http\Formatters\HealthFormatter;
use App\Http\Responses\JsonApiResponse;
use App\UseCases\CheckHealthUseCase;
use Illuminate\Http\JsonResponse;

final class HealthController
{
    public function index(CheckHealthUseCase $useCase): JsonResponse
    {
        $health = $useCase->execute();

        // 依存が落ちていれば 503。本文が返せていても 200 にしないこと。
        // ALB / ヘルスチェックはステータスコードで判定する。
        return JsonApiResponse::ok(
            HealthFormatter::format($health),
            $health->isHealthy() ? 200 : 503,
        );
    }

    /** プロセスの生存のみを見る（依存を確認しない）。 */
    public function live(): JsonResponse
    {
        return JsonApiResponse::ok(['status' => 'ok']);
    }
}
