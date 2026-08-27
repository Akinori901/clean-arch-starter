<?php

declare(strict_types=1);

namespace Tests\Unit;

use App\Helpers\DisplayNameHelper;
use PHPUnit\Framework\TestCase;

final class DisplayNameHelperTest extends TestCase
{
    public function test_uses_local_part_of_email(): void
    {
        $this->assertSame('taro', DisplayNameHelper::fromEmail('taro@example.com'));
    }

    public function test_returns_input_when_no_at_sign(): void
    {
        $this->assertSame('plain', DisplayNameHelper::fromEmail('plain'));
    }
}
