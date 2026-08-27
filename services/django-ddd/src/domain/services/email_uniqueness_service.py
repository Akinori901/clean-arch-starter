"""ドメインサービス（Domain Service）。

**単一のエンティティ・集約に属さない業務ルール**をここに置く。

「メールアドレスが他のユーザーと重複していないか」は、
特定の UserAccount 1 つを見ても判定できない（他の全アカウントとの関係で決まる）。
こういうルールを無理にエンティティのメソッドにすると、
エンティティが自分以外の集約を知ることになり、集約の境界が壊れる。

ドメインサービスは **状態を持たない**。判定するだけで、保存はしない。
"""
from __future__ import annotations

from domain.aggregates.user_account import UserAccount
from domain.exceptions import EmailAlreadyTakenError
from domain.repositories.user_repository import UserRepository
from domain.value_objects.email import Email


class EmailUniquenessService:
    """メールアドレスの一意性を判定するドメインサービス。"""

    def __init__(self, users: UserRepository) -> None:
        self._users = users

    def ensure_available(self, email: Email, *, exclude: UserAccount | None = None) -> None:
        """そのメールアドレスが使用可能か検証する。

        変更時は自分自身を除外する必要があるため exclude を受ける
        （自分のアドレスで「重複しています」と言われないように）。

        :raises EmailAlreadyTakenError: 既に他のユーザーが使用している
        """
        owner = self._users.find_by_email(email)
        if owner is None:
            return
        if exclude is not None and owner.id == exclude.id:
            return
        raise EmailAlreadyTakenError("このメールアドレスは既に使用されています")
