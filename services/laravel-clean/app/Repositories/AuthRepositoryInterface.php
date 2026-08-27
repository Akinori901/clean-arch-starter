<?php

declare(strict_types=1);

namespace App\Repositories;

use App\DataTransferObjects\AuthTokensDto;
use App\DataTransferObjects\VerifiedIdentityDto;

/**
 * 認証基盤の契約。
 *
 * Cognito という具体名をここに出さないこと。
 * Service は「認証できること」だけを知っていればよい。
 */
interface AuthRepositoryInterface
{
    /**
     * 認証情報を検証しトークンを発行する。
     *
     * @throws \App\Exceptions\AuthenticationFailedException
     */
    public function signIn(string $email, string $password): AuthTokensDto;

    /**
     * アクセストークンを検証し本人情報を返す。
     *
     * @throws \App\Exceptions\AuthenticationFailedException
     */
    public function verifyAccessToken(string $token): VerifiedIdentityDto;
}
