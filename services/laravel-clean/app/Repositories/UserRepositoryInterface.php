<?php

declare(strict_types=1);

namespace App\Repositories;

use App\DataTransferObjects\UserDto;

/**
 * ユーザーリポジトリの契約。
 *
 * **戻り値は必ず Dto。** Eloquent Model / Collection を返さないこと。
 * 返した瞬間に、ORM の都合が Service 層より上へ漏れ出す。
 */
interface UserRepositoryInterface
{
    public function findById(string $id): ?UserDto;

    public function findByEmail(string $email): ?UserDto;

    public function save(UserDto $user): UserDto;
}
