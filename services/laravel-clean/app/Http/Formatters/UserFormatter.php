<?php

declare(strict_types=1);

namespace App\Http\Formatters;

use App\DataTransferObjects\UserDto;

final class UserFormatter
{
    /** @return array<string, mixed> */
    public static function format(UserDto $user): array
    {
        return [
            'id' => $user->id,
            'email' => $user->email,
            'display_name' => $user->displayName,
            'is_active' => $user->isActive,
        ];
    }
}
