from __future__ import annotations

from domain.entities.user import User
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


def _user(uid: str = "sub-1", *, active: bool = True) -> User:
    return User(
        id=UserId(uid),
        email=Email("user@example.com"),
        display_name="user",
        is_active=active,
    )


def test_entity_equality_is_by_identity_only() -> None:
    a = _user("same")
    b = _user("same")
    b.display_name = "changed"
    # 値が違っても識別子が同じなら同じエンティティ
    assert a == b


def test_deactivated_user_cannot_sign_in() -> None:
    user = _user()
    assert user.can_sign_in() is True

    user.deactivate()
    assert user.can_sign_in() is False
