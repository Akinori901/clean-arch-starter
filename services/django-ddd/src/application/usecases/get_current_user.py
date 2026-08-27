"""アクセストークンから現在のユーザーを取得するユースケース。"""
from __future__ import annotations

from application.dto.auth_dto import CurrentUserOutput
from application.ports.authenticator import AuthenticatorPort
from domain.exceptions import UserNotFoundError
from domain.repositories.user_repository import UserRepository
from domain.value_objects.user_id import UserId


class GetCurrentUserUseCase:
    def __init__(
        self,
        authenticator: AuthenticatorPort,
        user_repository: UserRepository,
    ) -> None:
        self._authenticator = authenticator
        self._users = user_repository

    def execute(self, access_token: str) -> CurrentUserOutput:
        identity = self._authenticator.verify_access_token(access_token)

        account = self._users.find_by_id(UserId(identity.subject))
        if account is None:
            raise UserNotFoundError("ユーザーが見つかりません")

        return CurrentUserOutput(
            user_id=str(account.id),
            email=str(account.user.email),
            display_name=str(account.profile.display_name),
            is_active=account.user.is_active,
        )
