"""AuthenticatorPort の Cognito 実装。

boto3 と JWT 検証をここに閉じ込める。application 層は Cognito を知らない。

ローカル開発では COGNITO_ENDPOINT_URL に cognito-local を指すことで
同じコードパスのまま動く。`if local:` の分岐をアプリコードに書かないこと。
"""
from __future__ import annotations

import base64
import hashlib
import hmac

import boto3
import jwt
from botocore.exceptions import ClientError
from jwt import PyJWKClient

from application.ports.authenticator import (
    AuthenticatorPort,
    AuthTokens,
    VerifiedIdentity,
)
from domain.exceptions import AuthenticationFailedError
from domain.value_objects.email import Email


class CognitoAuthenticator(AuthenticatorPort):
    def __init__(
        self,
        *,
        user_pool_id: str,
        client_id: str,
        region: str,
        client_secret: str | None = None,
        endpoint_url: str | None = None,
        issuer_override: str | None = None,
        jwks_url_override: str | None = None,
    ) -> None:
        self._user_pool_id = user_pool_id
        self._client_id = client_id
        self._region = region
        self._client_secret = client_secret
        self._client = boto3.client(
            "cognito-idp", region_name=region, endpoint_url=endpoint_url
        )

        # JWKS はコールドスタート時に一度だけ解決し、以降は使い回す。
        # Lambda では実行環境が再利用されるため、ここでの再取得コストは償却される。
        #
        # issuer_override はローカルのエミュレータを指すためだけに使う。
        # 本番では必ず None にすること（未指定なら実 Cognito の issuer になる）。
        self._issuer = issuer_override or (
            f"https://cognito-idp.{region}.amazonaws.com/{user_pool_id}"
        )
        # JWKS の取得先と、トークンに刻まれる issuer は必ずしも一致しない。
        # ローカルのエミュレータは自分が公開している URL（localhost）を iss に刻む一方、
        # コンテナからは別ホスト名（cognito:9229）でしか到達できないため。
        # 本番では両方とも未指定でよい。
        self._jwk_client = PyJWKClient(
            jwks_url_override or f"{self._issuer}/.well-known/jwks.json"
        )

    def sign_in(self, email: Email, password: str) -> AuthTokens:
        params = {"USERNAME": str(email), "PASSWORD": password}
        if self._client_secret:
            params["SECRET_HASH"] = self._secret_hash(str(email))

        try:
            response = self._client.initiate_auth(
                ClientId=self._client_id,
                AuthFlow="USER_PASSWORD_AUTH",
                AuthParameters=params,
            )
        except ClientError as exc:
            code = exc.response.get("Error", {}).get("Code", "")
            # 「ユーザーが存在しない」と「パスワードが違う」を区別して返さないこと。
            # 区別するとアカウント列挙に使われる。
            #
            # InvalidPasswordException / InvalidParameterException も認証失敗に含める。
            # これらを漏らすと 500 になり、認証エラーが障害として扱われてしまう。
            if code in {
                "NotAuthorizedException",
                "UserNotFoundException",
                "InvalidPasswordException",
                "InvalidParameterException",
                "UserNotConfirmedException",
            }:
                raise AuthenticationFailedError(
                    "メールアドレスまたはパスワードが正しくありません"
                ) from exc
            raise

        # MFA 等で追加ステップが必要な場合、AuthenticationResult は返らない
        result = response.get("AuthenticationResult")
        if not result:
            raise AuthenticationFailedError(
                f"追加の認証ステップが必要です: {response.get('ChallengeName')}"
            )

        return AuthTokens(
            access_token=result["AccessToken"],
            id_token=result["IdToken"],
            refresh_token=result.get("RefreshToken", ""),
            # ExpiresIn は実 Cognito では必ず返るが、エミュレータでは
            # 省略されることがある。ここで落とすと本番だけ動く実装になる。
            expires_in=int(result.get("ExpiresIn", 3600)),
        )

    def verify_access_token(self, token: str) -> VerifiedIdentity:
        try:
            signing_key = self._jwk_client.get_signing_key_from_jwt(token)
            claims = jwt.decode(
                token,
                signing_key.key,
                algorithms=["RS256"],
                issuer=self._issuer,
                # Cognito のアクセストークンには aud が無く、client_id が入る。
                # そのため aud 検証は無効化し、下で client_id を明示的に照合する。
                options={"verify_aud": False},
            )
        except jwt.PyJWTError as exc:
            raise AuthenticationFailedError("トークンが無効です") from exc

        if claims.get("token_use") != "access":
            raise AuthenticationFailedError("アクセストークンではありません")
        if claims.get("client_id") != self._client_id:
            raise AuthenticationFailedError("発行先クライアントが一致しません")

        return VerifiedIdentity(
            subject=claims["sub"],
            # アクセストークンに email は含まれないことがある。
            # 必要なら id_token 側か users テーブルから引く。
            email=claims.get("email", ""),
        )

    def _secret_hash(self, username: str) -> str:
        digest = hmac.new(
            key=(self._client_secret or "").encode("utf-8"),
            msg=(username + self._client_id).encode("utf-8"),
            digestmod=hashlib.sha256,
        ).digest()
        return base64.b64encode(digest).decode()
