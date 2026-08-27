<?php

declare(strict_types=1);

namespace App\Exceptions;

use RuntimeException;

/**
 * 認証失敗。
 *
 * HTTP ステータスコードをここに持ち込まないこと。
 * 「認証に失敗した」は業務の語彙、「401」は Controller の語彙である。
 */
final class AuthenticationFailedException extends RuntimeException
{
}
