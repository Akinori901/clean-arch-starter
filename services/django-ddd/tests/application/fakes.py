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
from domain.aggregates.user_account import UserAccount
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
    """集約ルート単位で出し入れする。DDD の Repository は集約ごとに 1 つ。"""

    def __init__(self, accounts: list[UserAccount] | None = None) -> None:
        self._store: dict[str, UserAccount] = {
            str(a.id): a for a in (accounts or [])
        }

    def find_by_id(self, user_id: UserId) -> UserAccount | None:
        return self._store.get(str(user_id))

    def find_by_email(self, email: Email) -> UserAccount | None:
        return next(
            (a for a in self._store.values() if str(a.user.email) == str(email)),
            None,
        )

    def save(self, account: UserAccount) -> UserAccount:
        self._store[str(account.id)] = account
        return account


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
