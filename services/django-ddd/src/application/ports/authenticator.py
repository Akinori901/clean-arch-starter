"""認証プロバイダの契約（ポート）。

Cognito という具体名をここに出さないこと。
application は「認証できること」だけを知っていればよく、
それが Cognito なのか Auth0 なのかは infrastructure の選択である。
"""
from __future__ import annotations

from abc import ABC, abstractmethod
from dataclasses import dataclass

from domain.value_objects.email import Email


@dataclass(frozen=True)
class AuthTokens:
    """認証成功時に発行されるトークン群。"""

    access_token: str
    id_token: str
    refresh_token: str
    expires_in: int


@dataclass(frozen=True)
class VerifiedIdentity:
    """検証済みトークンから取り出した本人情報。"""

    subject: str
    email: str


class AuthenticatorPort(ABC):
    @abstractmethod
    def sign_in(self, email: Email, password: str) -> AuthTokens:
        """認証情報を検証しトークンを発行する。

        失敗時は domain.exceptions.AuthenticationFailedError を送出する。
        """

    @abstractmethod
    def verify_access_token(self, token: str) -> VerifiedIdentity:
        """アクセストークンを検証し本人情報を返す。"""
