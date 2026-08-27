"""サインインユースケース。

1ファイル1ユースケース。公開メソッドは execute() のみ。
依存はすべてコンストラクタ注入で受け取る。ここで具象を import しないこと。
"""
from __future__ import annotations

from application.dto.auth_dto import SignInInput, SignInOutput
from application.ports.authenticator import AuthenticatorPort
from domain.entities.user import User
from domain.exceptions import AuthenticationFailedError
from domain.repositories.user_repository import UserRepository
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


class SignInUseCase:
    def __init__(
        self,
        authenticator: AuthenticatorPort,
        user_repository: UserRepository,
    ) -> None:
        self._authenticator = authenticator
        self._users = user_repository

    def execute(self, input_: SignInInput) -> SignInOutput:
        email = Email(input_.email)

        # 1. 認証基盤（Cognito）で認証する
        tokens = self._authenticator.sign_in(email, input_.password)

        # 2. 検証済みトークンから本人を特定する
        identity = self._authenticator.verify_access_token(tokens.access_token)
        user_id = UserId(identity.subject)

        # 3. ローカル側のユーザーを引く。初回サインインなら作る
        #    （Cognito が正、ローカルはプロフィールの保持のみを担う）
        user = self._users.find_by_id(user_id)
        if user is None:
            user = self._users.save(
                User(
                    id=user_id,
                    email=email,
                    display_name=email.value.split("@")[0],
                )
            )

        # 4. 無効化されたアカウントは、Cognito 側が通しても拒否する
        if not user.can_sign_in():
            raise AuthenticationFailedError("このアカウントは無効化されています")

        return SignInOutput(
            access_token=tokens.access_token,
            id_token=tokens.id_token,
            refresh_token=tokens.refresh_token,
            expires_in=tokens.expires_in,
            user_id=str(user.id),
            email=str(user.email),
            display_name=user.display_name,
        )
