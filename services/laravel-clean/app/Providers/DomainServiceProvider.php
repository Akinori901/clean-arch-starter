<?php

declare(strict_types=1);

namespace App\Providers;

use App\Repositories\AuthRepositoryInterface;
use App\Repositories\CognitoAuthRepository;
use App\Repositories\HealthRepository;
use App\Repositories\HealthRepositoryInterface;
use App\Repositories\UserRepository;
use App\Repositories\UserRepositoryInterface;
use Aws\CognitoIdentityProvider\CognitoIdentityProviderClient;
use Aws\S3\S3Client;
use Illuminate\Support\ServiceProvider;

/**
 * 契約 → 実装の結線。
 *
 * **具象クラスを bind してよいのはここだけ。**
 * Controller / UseCase / Service が具象を直接 new した時点で層の境界が壊れる。
 */
final class DomainServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        $this->app->singleton(UserRepositoryInterface::class, UserRepository::class);

        // endpoint はローカル（SeaweedFS / cognito-local）のときだけ設定する。
        // 本番では null にして AWS の既定エンドポイントを使う。
        $this->app->singleton(S3Client::class, fn (): S3Client => new S3Client(array_filter([
            'version' => 'latest',
            'region' => config('services.aws.region'),
            'endpoint' => config('services.s3.endpoint') ?: null,
            'use_path_style_endpoint' => (bool) config('services.s3.endpoint'),
        ], static fn ($v): bool => $v !== null)));

        $this->app->singleton(
            CognitoIdentityProviderClient::class,
            fn (): CognitoIdentityProviderClient => new CognitoIdentityProviderClient(array_filter([
                'version' => 'latest',
                'region' => config('services.aws.region'),
                'endpoint' => config('services.cognito.endpoint') ?: null,
            ], static fn ($v): bool => $v !== null)),
        );

        $this->app->singleton(
            AuthRepositoryInterface::class,
            fn ($app): CognitoAuthRepository => new CognitoAuthRepository(
                client: $app->make(CognitoIdentityProviderClient::class),
                userPoolId: (string) config('services.cognito.user_pool_id'),
                clientId: (string) config('services.cognito.client_id'),
                region: (string) config('services.aws.region'),
                clientSecret: config('services.cognito.client_secret') ?: null,
            ),
        );

        $this->app->singleton(
            HealthRepositoryInterface::class,
            fn ($app): HealthRepository => new HealthRepository(
                s3: $app->make(S3Client::class),
                cognito: $app->make(CognitoIdentityProviderClient::class),
                bucket: (string) config('services.s3.bucket'),
                userPoolId: (string) config('services.cognito.user_pool_id'),
            ),
        );
    }
}
