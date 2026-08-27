<?php

declare(strict_types=1);

return [
    'aws' => [
        'region' => env('AWS_REGION', 'ap-northeast-1'),
    ],

    'cognito' => [
        'user_pool_id' => env('COGNITO_USER_POOL_ID', ''),
        'client_id' => env('COGNITO_CLIENT_ID', ''),
        'client_secret' => env('COGNITO_CLIENT_SECRET', ''),
        // ローカルの cognito-local を指すときのみ設定する
        'endpoint' => env('COGNITO_ENDPOINT_URL', ''),
    ],

    's3' => [
        'bucket' => env('S3_BUCKET', 'app-static'),
        // ローカルの SeaweedFS を指すときのみ設定する
        'endpoint' => env('S3_ENDPOINT_URL', ''),
    ],
];
