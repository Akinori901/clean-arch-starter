"""ドメインサービスのテスト。

「他のユーザーと重複していないか」は単一集約では判定できない。
そういう規則をドメインサービスに置く。
"""
from __future__ import annotations

import pytest

from domain.aggregates.user_account import UserAccount
from domain.exceptions import EmailAlreadyTakenError
from domain.services.email_uniqueness_service import EmailUniquenessService
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId
from tests.application.fakes import InMemoryUserRepository


def _account(uid: str, email: str) -> UserAccount:
    return UserAccount.register(UserId(uid), Email(email))


def test_unused_email_is_available() -> None:
    service = EmailUniquenessService(InMemoryUserRepository())
    service.ensure_available(Email("free@example.com"))  # 例外が出なければ合格


def test_email_taken_by_another_user_is_rejected() -> None:
    repo = InMemoryUserRepository([_account("sub-1", "taken@example.com")])
    service = EmailUniquenessService(repo)

    with pytest.raises(EmailAlreadyTakenError):
        service.ensure_available(Email("taken@example.com"))


def test_own_email_is_allowed_when_excluded() -> None:
    """変更時に自分自身のアドレスで弾かれないこと。"""
    mine = _account("sub-1", "mine@example.com")
    service = EmailUniquenessService(InMemoryUserRepository([mine]))

    service.ensure_available(Email("mine@example.com"), exclude=mine)
