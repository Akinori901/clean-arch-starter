from __future__ import annotations

import pytest

from application.dto.auth_dto import SignInInput
from application.usecases.sign_in import SignInUseCase
from domain.aggregates.user_account import UserAccount
from domain.exceptions import AuthenticationFailedError
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId
from tests.application.fakes import FakeAuthenticator, InMemoryUserRepository


def test_first_sign_in_provisions_local_user() -> None:
    repo = InMemoryUserRepository()
    usecase = SignInUseCase(FakeAuthenticator(subject="sub-1"), repo)

    output = usecase.execute(
        SignInInput(email="user@example.com", password="correct-pass")
    )

    assert output.user_id == "sub-1"
    assert output.display_name == "user"
    # Cognito 側にしか居なかったユーザーがローカルにも作られている
    assert repo.find_by_id(UserId("sub-1")) is not None


def test_wrong_password_is_rejected() -> None:
    usecase = SignInUseCase(FakeAuthenticator(), InMemoryUserRepository())

    with pytest.raises(AuthenticationFailedError):
        usecase.execute(SignInInput(email="user@example.com", password="wrong"))


def test_deactivated_user_is_rejected_even_if_cognito_accepts() -> None:
    """Cognito が通してもローカルで無効化されていれば拒否する。

    認証基盤の状態と業務上の有効/無効は別の関心事である。
    """
    account = UserAccount.register(UserId("sub-1"), Email("user@example.com"))
    account.deactivate()
    repo = InMemoryUserRepository([account])
    usecase = SignInUseCase(FakeAuthenticator(subject="sub-1"), repo)

    with pytest.raises(AuthenticationFailedError, match="無効化"):
        usecase.execute(SignInInput(email="user@example.com", password="correct-pass"))


def test_invalid_email_format_is_rejected_before_auth() -> None:
    from domain.exceptions import InvalidEmailError

    usecase = SignInUseCase(FakeAuthenticator(), InMemoryUserRepository())
    with pytest.raises(InvalidEmailError):
        usecase.execute(SignInInput(email="not-an-email", password="correct-pass"))
