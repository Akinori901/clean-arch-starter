"""UserRepository 契約。

DDD では **Repository は集約ルート単位で 1 つ**作る。
Profile 用の Repository を別に作らないこと。
集約の内部エンティティは、常にルート（UserAccount）ごと出し入れする。

ここには ABC のみを置き、実装は infrastructure/django_orm/repositories/ に置く。
戻り値は必ず**集約**であること。Django Model や QuerySet を返さない。
それを返した瞬間に、ORM の都合が domain を越えて漏れ出す。
"""
from __future__ import annotations

from abc import ABC, abstractmethod

from domain.aggregates.user_account import UserAccount
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId


class UserRepository(ABC):
    """UserAccount 集約のリポジトリ。"""

    @abstractmethod
    def find_by_id(self, user_id: UserId) -> UserAccount | None:
        """集約ルートの ID で取得する。存在しなければ None。"""

    @abstractmethod
    def find_by_email(self, email: Email) -> UserAccount | None:
        """メールアドレスで取得する。存在しなければ None。"""

    @abstractmethod
    def save(self, account: UserAccount) -> UserAccount:
        """集約まるごと永続化する（新規・更新の両方）。

        集約はトランザクションの境界。
        内部エンティティだけを部分的に保存するメソッドを増やさないこと。
        """
