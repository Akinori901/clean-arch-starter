<?php

declare(strict_types=1);

namespace App\UseCases;

use App\DataTransferObjects\HealthDto;
use App\Services\HealthService;

final readonly class CheckHealthUseCase
{
    public function __construct(
        private HealthService $health,
    ) {
    }

    public function execute(): HealthDto
    {
        return $this->health->inspect();
    }
}
