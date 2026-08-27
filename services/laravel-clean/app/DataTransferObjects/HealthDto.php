<?php

declare(strict_types=1);

namespace App\DataTransferObjects;

use App\Enums\ComponentState;

final readonly class ComponentHealthDto
{
    public function __construct(
        public string $name,
        public ComponentState $state,
        public string $detail = '',
    ) {
    }
}

final readonly class HealthDto
{
    /** @param list<ComponentHealthDto> $components */
    public function __construct(
        public array $components,
    ) {
    }

    /** 全構成要素が Up のときのみ健全とみなす。 */
    public function isHealthy(): bool
    {
        foreach ($this->components as $component) {
            if ($component->state !== ComponentState::Up) {
                return false;
            }
        }

        return true;
    }
}
