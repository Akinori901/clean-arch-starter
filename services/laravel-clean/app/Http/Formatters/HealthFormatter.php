<?php

declare(strict_types=1);

namespace App\Http\Formatters;

use App\DataTransferObjects\HealthDto;

final class HealthFormatter
{
    /** @return array<string, mixed> */
    public static function format(HealthDto $health): array
    {
        return [
            'healthy' => $health->isHealthy(),
            'components' => array_map(
                static fn ($c): array => [
                    'name' => $c->name,
                    'state' => $c->state->value,
                    'detail' => $c->detail,
                ],
                $health->components,
            ),
        ];
    }
}
