<?php

declare(strict_types=1);

namespace App\DataTransferObjects;

/**
 * ユーザーの DTO。
 *
 * **Model に一切依存しない。** `fromModel()` のようなファクトリをここに置かないこと。
 * Model → Dto の変換は Repository の中で完結させ、
 * Dto を依存グラフの末端に保つ（deptrac の Dto 層は依存先ゼロ）。
 */
final readonly class UserDto
{
    public function __construct(
        public string $id,
        public string $email,
        public string $displayName,
        public bool $isActive,
    ) {
    }

    /**
     * 業務上サインインを許可してよいか。
     *
     * 認証基盤（Cognito）が通しても、こちらで無効化されていれば拒否する。
     */
    public function canSignIn(): bool
    {
        return $this->isActive;
    }
}
