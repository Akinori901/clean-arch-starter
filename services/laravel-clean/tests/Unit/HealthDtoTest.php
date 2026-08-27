<?php

declare(strict_types=1);

namespace Tests\Unit;

use App\DataTransferObjects\ComponentHealthDto;
use App\DataTransferObjects\HealthDto;
use App\Enums\ComponentState;
use PHPUnit\Framework\TestCase;

/**
 * Dto のテストは Laravel を起動せずに動く（TestCase であって
 * Illuminate の TestCase ではない）。層を分けた実利がここに出る。
 */
final class HealthDtoTest extends TestCase
{
    public function test_all_up_is_healthy(): void
    {
        $health = new HealthDto([
            new ComponentHealthDto('database', ComponentState::Up),
            new ComponentHealthDto('cognito', ComponentState::Up),
        ]);

        $this->assertTrue($health->isHealthy());
    }

    public function test_single_down_makes_whole_unhealthy(): void
    {
        $health = new HealthDto([
            new ComponentHealthDto('database', ComponentState::Up),
            new ComponentHealthDto('cognito', ComponentState::Down, 'timeout'),
        ]);

        $this->assertFalse($health->isHealthy());
    }

    public function test_empty_is_healthy(): void
    {
        $this->assertTrue((new HealthDto([]))->isHealthy());
    }
}
