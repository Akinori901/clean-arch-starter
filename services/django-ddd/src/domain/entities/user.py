"""User エンティティ。

エンティティは「同一性」を持つ。値が変わっても UserId が同じなら同じ User。
ORM の都合（created_at の自動採番、外部キーの遅延ロード等）を
ここに持ち込まないこと。それは infrastructure の関心事。
"""
from __future__ import annotations

from dataclasses import dataclass

from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


@dataclass
class User:
    id: UserId
    email: Email
    display_name: str
    is_active: bool = True

    def __eq__(self, other: object) -> bool:
        # エンティティの等価性は識別子のみで決まる
        if not isinstance(other, User):
            return NotImplemented
        return self.id == other.id

    def __hash__(self) -> int:
        return hash(self.id)

    def deactivate(self) -> None:
        """アカウントを無効化する。"""
        self.is_active = False

    def can_sign_in(self) -> bool:
        """サインイン可能かを判定する（ドメインの規則）。"""
        return self.is_active
