<?php

declare(strict_types=1);

namespace App\UseCases;

use App\DataTransferObjects\UserDto;
use App\Services\AuthService;

final readonly class GetCurrentUserUseCase
{
    public function __construct(
        private AuthService $auth,
    ) {
    }

    public function execute(string $accessToken): UserDto
    {
        return $this->auth->resolveUser($accessToken);
    }
}
