<?php

declare(strict_types=1);

namespace App\DataTransferObjects;

final readonly class VerifiedIdentityDto
{
    public function __construct(
        public string $subject,
        public string $email,
    ) {
    }
}
