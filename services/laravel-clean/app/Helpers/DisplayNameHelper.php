<?php

declare(strict_types=1);

namespace App\Helpers;

/**
 * 表示名にまつわる純粋関数。
 *
 * Helper は Model / DB に依存しない。依存してよいのは Enum のみ。
 */
final class DisplayNameHelper
{
    /** メールアドレスのローカル部を既定の表示名にする。 */
    public static function fromEmail(string $email): string
    {
        $localPart = strstr($email, '@', true);

        return $localPart === false ? $email : $localPart;
    }
}
