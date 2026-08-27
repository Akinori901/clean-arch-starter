<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Model;

/**
 * Eloquent Model。
 *
 * **use してよいのは Repository 層だけ。** deptrac がこれを強制する。
 * Model はテーブルの写像であり、ビジネスルールを持たせない。
 *
 * @property string $id
 * @property string $email
 * @property string $display_name
 * @property bool   $is_active
 */
final class User extends Model
{
    protected $table = 'users';

    // Cognito の sub をそのまま主キーにする（採番を Cognito へ委ねる）
    protected $keyType = 'string';

    public $incrementing = false;

    protected $fillable = ['id', 'email', 'display_name', 'is_active'];

    protected $casts = ['is_active' => 'bool'];
}
