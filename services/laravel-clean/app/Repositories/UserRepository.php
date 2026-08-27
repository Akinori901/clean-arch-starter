<?php

declare(strict_types=1);

namespace App\Repositories;

use App\DataTransferObjects\UserDto;
use App\Models\User;

/**
 * UserRepositoryInterface の Eloquent 実装。
 *
 * Model を use してよい唯一の層。
 * **返す直前に必ず Model → Dto へ変換する。**
 */
final readonly class UserRepository implements UserRepositoryInterface
{
    public function findById(string $id): ?UserDto
    {
        $row = User::query()->find($id);

        return $row === null ? null : $this->toDto($row);
    }

    public function findByEmail(string $email): ?UserDto
    {
        $row = User::query()->where('email', $email)->first();

        return $row === null ? null : $this->toDto($row);
    }

    public function save(UserDto $user): UserDto
    {
        $row = User::query()->updateOrCreate(
            ['id' => $user->id],
            [
                'email' => $user->email,
                'display_name' => $user->displayName,
                'is_active' => $user->isActive,
            ],
        );

        return $this->toDto($row);
    }

    /** Model → Dto 変換。この境界で ORM の都合を断ち切る。 */
    private function toDto(User $row): UserDto
    {
        return new UserDto(
            id: $row->id,
            email: $row->email,
            displayName: $row->display_name,
            isActive: $row->is_active,
        );
    }
}
