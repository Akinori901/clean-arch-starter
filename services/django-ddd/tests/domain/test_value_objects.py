"""domain のテストは Django を一切 import せずに動く。

これが DDD の実利。ドメインのテストに DB もフレームワークも要らない。
"""
from __future__ import annotations

from dataclasses import FrozenInstanceError

import pytest

from domain.exceptions import InvalidEmailError, InvalidUserIdError
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


def test_email_accepts_valid_address() -> None:
    assert str(Email("user@example.com")) == "user@example.com"


@pytest.mark.parametrize("bad", ["", "no-at-sign", "a@b", "a b@example.com"])
def test_email_rejects_invalid_address(bad: str) -> None:
    with pytest.raises(InvalidEmailError):
        Email(bad)


def test_email_equality_is_by_value() -> None:
    assert Email("a@example.com") == Email("a@example.com")


def test_email_is_immutable() -> None:
    email = Email("a@example.com")
    with pytest.raises(FrozenInstanceError):
        email.value = "b@example.com"  # type: ignore[misc]


def test_user_id_rejects_blank() -> None:
    with pytest.raises(InvalidUserIdError):
        UserId("   ")
