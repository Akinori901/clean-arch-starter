<?php

declare(strict_types=1);

namespace App\Repositories;

/**
 * 外部依存の疎通確認の契約。
 *
 * 「確認できること」だけを表す。成否は例外で表現し、bool を返さない。
 */
interface HealthRepositoryInterface
{
    /** DB への疎通確認。失敗時は例外を送出する。 */
    public function pingDatabase(): void;

    /** オブジェクトストレージ（S3 / SeaweedFS）への疎通確認。 */
    public function pingObjectStorage(): void;

    /** Cognito への疎通確認。 */
    public function pingCognito(): void;
}
