<?php

declare(strict_types=1);

namespace App\Services;

use App\DataTransferObjects\ComponentHealthDto;
use App\DataTransferObjects\HealthDto;
use App\Enums\ComponentState;
use App\Repositories\HealthRepositoryInterface;
use Throwable;

final readonly class HealthService
{
    public function __construct(
        private HealthRepositoryInterface $health,
    ) {
    }

    public function inspect(): HealthDto
    {
        return new HealthDto([
            $this->probe('database', fn () => $this->health->pingDatabase()),
            $this->probe('object_storage', fn () => $this->health->pingObjectStorage()),
            $this->probe('cognito', fn () => $this->health->pingCognito()),
        ]);
    }

    /**
     * 1つ落ちても他の確認は続ける。全体像が見えないと切り分けができない。
     */
    private function probe(string $name, callable $check): ComponentHealthDto
    {
        try {
            $check();

            return new ComponentHealthDto($name, ComponentState::Up);
        } catch (Throwable $e) {
            return new ComponentHealthDto(
                $name,
                ComponentState::Down,
                mb_substr($e->getMessage(), 0, 200),
            );
        }
    }
}
