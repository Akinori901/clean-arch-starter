<?php

declare(strict_types=1);

namespace App\DataTransferObjects;

final readonly class AuthTokensDto
{
    public function __construct(
        public string $accessToken,
        public string $idToken,
        public string $refreshToken,
        public int $expiresIn,
    ) {
    }
}
