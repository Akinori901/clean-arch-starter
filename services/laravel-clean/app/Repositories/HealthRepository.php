<?php

declare(strict_types=1);

namespace App\Repositories;

use Aws\CognitoIdentityProvider\CognitoIdentityProviderClient;
use Aws\S3\S3Client;
use Illuminate\Support\Facades\DB;

/**
 * HealthRepositoryInterface の実装。
 *
 * DB ファサードと AWS SDK をここに閉じ込める。
 */
final readonly class HealthRepository implements HealthRepositoryInterface
{
    public function __construct(
        private S3Client $s3,
        private CognitoIdentityProviderClient $cognito,
        private string $bucket,
        private string $userPoolId,
    ) {
    }

    public function pingDatabase(): void
    {
        DB::select('SELECT 1');
    }

    public function pingObjectStorage(): void
    {
        // オブジェクト一覧ではなく HeadBucket を使う。
        // 必要な権限が最小で済み、バケットの中身の量に影響されない。
        $this->s3->headBucket(['Bucket' => $this->bucket]);
    }

    public function pingCognito(): void
    {
        $this->cognito->describeUserPool(['UserPoolId' => $this->userPoolId]);
    }
}
