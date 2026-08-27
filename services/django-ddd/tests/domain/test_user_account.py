"""集約（Aggregate）のテスト。

集約は「不変条件を守る単位」。ここでは
「無効なアカウントは変更できない」「自己紹介は500文字まで」が不変条件。
"""
from __future__ import annotations

import pytest

from domain.aggregates.user_account import UserAccount
from domain.exceptions import ProfileRuleViolationError
from domain.value_objects.display_name import DisplayName
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


def _account(email: str = "taro@example.com") -> UserAccount:
    return UserAccount.register(UserId("sub-1"), Email(email))


def test_register_derives_display_name_from_email() -> None:
    account = _account()
    assert str(account.profile.display_name) == "taro"
    # 集約ルートと内部エンティティで表示名が整合している
    assert account.user.display_name == "taro"


def test_register_records_domain_event() -> None:
    assert _account().events == ["UserRegistered"]


def test_pull_events_drains_once() -> None:
    """イベントは取り出したら消える（二重発行を防ぐ）。"""
    account = _account()
    assert account.pull_events() == ["UserRegistered"]
    assert account.pull_events() == []


def test_rename_updates_both_root_and_internal_entity() -> None:
    account = _account()
    account.pull_events()

    account.rename(DisplayName("新しい名前"))

    assert str(account.profile.display_name) == "新しい名前"
    assert account.user.display_name == "新しい名前"
    assert account.pull_events() == ["UserRenamed"]


def test_deactivated_account_cannot_be_renamed() -> None:
    """集約の不変条件。ここを破る操作はルートが拒否する。"""
    account = _account()
    account.deactivate()

    with pytest.raises(ProfileRuleViolationError):
        account.rename(DisplayName("新しい名前"))


def test_bio_length_is_enforced_by_aggregate() -> None:
    account = _account()
    account.update_bio("あ" * 500)  # 上限ちょうどは通る

    with pytest.raises(ProfileRuleViolationError):
        account.update_bio("あ" * 501)
