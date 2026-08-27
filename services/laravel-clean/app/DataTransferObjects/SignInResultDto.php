<?php

declare(strict_types=1);

namespace App\DataTransferObjects;

final readonly class SignInResultDto
{
    public function __construct(
        public AuthTokensDto $tokens,
        public UserDto $user,
    ) {
    }
}
