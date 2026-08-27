<?php

declare(strict_types=1);

namespace App\Repositories;

use App\DataTransferObjects\AuthTokensDto;
use App\DataTransferObjects\VerifiedIdentityDto;
use App\Exceptions\AuthenticationFailedException;
use Aws\CognitoIdentityProvider\CognitoIdentityProviderClient;
use Aws\Exception\AwsException;
use Firebase\JWT\JWK;
use Firebase\JWT\JWT;
use Illuminate\Support\Facades\Cache;

/**
 * AuthRepositoryInterface の Cognito 実装。
 *
 * AWS SDK と JWT 検証をここに閉じ込める。
 * ローカルでは endpoint に cognito-local を指すだけで同じコードが動く。
 */
final readonly class CognitoAuthRepository implements AuthRepositoryInterface
{
    public function __construct(
        private CognitoIdentityProviderClient $client,
        private string $userPoolId,
        private string $clientId,
        private string $region,
        private ?string $clientSecret = null,
    ) {
    }

    public function signIn(string $email, string $password): AuthTokensDto
    {
        $params = ['USERNAME' => $email, 'PASSWORD' => $password];
        if ($this->clientSecret !== null && $this->clientSecret !== '') {
            $params['SECRET_HASH'] = $this->secretHash($email);
        }

        try {
            $response = $this->client->initiateAuth([
                'ClientId' => $this->clientId,
                'AuthFlow' => 'USER_PASSWORD_AUTH',
                'AuthParameters' => $params,
            ]);
        } catch (AwsException $e) {
            // 「ユーザーが存在しない」と「パスワードが違う」を区別して返さないこと。
            // 区別するとアカウント列挙に利用される。
            if (in_array($e->getAwsErrorCode(), ['NotAuthorizedException', 'UserNotFoundException'], true)) {
                throw new AuthenticationFailedException(
                    'メールアドレスまたはパスワードが正しくありません',
                );
            }
            throw $e;
        }

        $result = $response['AuthenticationResult'] ?? null;
        if ($result === null) {
            // MFA 等で追加ステップが要求された場合
            throw new AuthenticationFailedException(
                sprintf('追加の認証ステップが必要です: %s', $response['ChallengeName'] ?? 'unknown'),
            );
        }

        return new AuthTokensDto(
            accessToken: $result['AccessToken'],
            idToken: $result['IdToken'],
            refreshToken: $result['RefreshToken'] ?? '',
            expiresIn: (int) $result['ExpiresIn'],
        );
    }

    public function verifyAccessToken(string $token): VerifiedIdentityDto
    {
        try {
            $claims = (array) JWT::decode($token, JWK::parseKeySet($this->jwks()));
        } catch (\Throwable $e) {
            throw new AuthenticationFailedException('トークンが無効です', 0, $e);
        }

        if (($claims['iss'] ?? null) !== $this->issuer()) {
            throw new AuthenticationFailedException('発行元が一致しません');
        }
        if (($claims['token_use'] ?? null) !== 'access') {
            throw new AuthenticationFailedException('アクセストークンではありません');
        }
        // Cognito のアクセストークンには aud が無く client_id が入る
        if (($claims['client_id'] ?? null) !== $this->clientId) {
            throw new AuthenticationFailedException('発行先クライアントが一致しません');
        }

        return new VerifiedIdentityDto(
            subject: (string) $claims['sub'],
            // アクセストークンに email は含まれないことがある
            email: (string) ($claims['email'] ?? ''),
        );
    }

    /**
     * JWKS はキャッシュする。毎リクエスト取りに行くと Cognito 側の
     * レート制限に当たり、レイテンシもそのぶん増える。
     *
     * @return array<string, mixed>
     */
    private function jwks(): array
    {
        return Cache::remember(
            'cognito:jwks:'.$this->userPoolId,
            now()->addHours(12),
            fn (): array => json_decode(
                (string) file_get_contents($this->issuer().'/.well-known/jwks.json'),
                true,
                flags: JSON_THROW_ON_ERROR,
            ),
        );
    }

    private function issuer(): string
    {
        return sprintf('https://cognito-idp.%s.amazonaws.com/%s', $this->region, $this->userPoolId);
    }

    private function secretHash(string $username): string
    {
        return base64_encode(
            hash_hmac('sha256', $username.$this->clientId, (string) $this->clientSecret, true),
        );
    }
}
