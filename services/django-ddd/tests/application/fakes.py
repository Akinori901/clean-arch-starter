"""テスト用のポート実装（フェイク）。

ポートが抽象になっていることの実利がここに出る。
Cognito も MySQL も無しで、ユースケースを完全にテストできる。
"""
from __future__ import annotations

from application.ports.authenticator import (
    AuthenticatorPort,
    AuthTokens,
    VerifiedIdentity,
)
from application.ports.health_probe import HealthProbePort
from domain.entities.user import User
from domain.exceptions import AuthenticationFailedError
from domain.repositories.user_repository import UserRepository
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


class FakeAuthenticator(AuthenticatorPort):
    def __init__(self, *, subject: str = "sub-1", password: str = "correct-pass"):
        self._subject = subject
        self._password = password

    def sign_in(self, email: Email, password: str) -> AuthTokens:
        if password != self._password:
            raise AuthenticationFailedError(
                "メールアドレスまたはパスワードが正しくありません"
            )
        return AuthTokens(
            access_token=f"access-{self._subject}",
            id_token="id-token",
            refresh_token="refresh-token",
            expires_in=3600,
        )

    def verify_access_token(self, token: str) -> VerifiedIdentity:
        if not token.startswith("access-"):
            raise AuthenticationFailedError("トークンが無効です")
        return VerifiedIdentity(
            subject=token.removeprefix("access-"), email="user@example.com"
        )


class InMemoryUserRepository(UserRepository):
    def __init__(self, users: list[User] | None = None) -> None:
        self._store: dict[str, User] = {str(u.id): u for u in (users or [])}

    def find_by_id(self, user_id: UserId) -> User | None:
        return self._store.get(str(user_id))

    def find_by_email(self, email: Email) -> User | None:
        return next(
            (u for u in self._store.values() if str(u.email) == str(email)), None
        )

    def save(self, user: User) -> User:
        self._store[str(user.id)] = user
        return user


class StubProbe(HealthProbePort):
    def __init__(self, name: str, *, fails: bool = False) -> None:
        self._name = name
        self._fails = fails

    @property
    def component_name(self) -> str:
        return self._name

    def check(self) -> None:
        if self._fails:
            raise RuntimeError("connection refused")
