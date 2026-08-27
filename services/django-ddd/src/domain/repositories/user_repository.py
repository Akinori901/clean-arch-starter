"""UserRepository 契約。

ここには ABC のみを置く。実装は infrastructure/django_orm/repositories/ にある。
戻り値は必ず「エンティティ」であること。Django Model や QuerySet を返さない。
それを返した瞬間に、ORM の都合が domain を越えて漏れ出す。
"""
from __future__ import annotations

from abc import ABC, abstractmethod

from domain.entities.user import User
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


class UserRepository(ABC):
    @abstractmethod
    def find_by_id(self, user_id: UserId) -> User | None:
        """ID でユーザーを取得する。存在しなければ None。"""

    @abstractmethod
    def find_by_email(self, email: Email) -> User | None:
        """Email でユーザーを取得する。存在しなければ None。"""

    @abstractmethod
    def save(self, user: User) -> User:
        """ユーザーを永続化する（新規・更新の両方）。"""
