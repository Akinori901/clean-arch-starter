"""UserRepository の Django 実装。

Model を扱ってよい唯一の場所。
**戻り値を返す直前に、必ず Model → エンティティへ変換する。**
Model をそのまま返すと、呼び出し元が ORM に依存してしまう。
"""
from __future__ import annotations

from domain.entities.user import User
from domain.repositories.user_repository import UserRepository
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId
from infrastructure.django_orm.models import UserModel


class DjangoUserRepository(UserRepository):
    def find_by_id(self, user_id: UserId) -> User | None:
        row = UserModel.objects.filter(pk=str(user_id)).first()
        return self._to_entity(row) if row else None

    def find_by_email(self, email: Email) -> User | None:
        row = UserModel.objects.filter(email=str(email)).first()
        return self._to_entity(row) if row else None

    def save(self, user: User) -> User:
        row, _ = UserModel.objects.update_or_create(
            pk=str(user.id),
            defaults={
                "email": str(user.email),
                "display_name": user.display_name,
                "is_active": user.is_active,
            },
        )
        return self._to_entity(row)

    @staticmethod
    def _to_entity(row: UserModel) -> User:
        """Model → エンティティ変換。この境界で ORM の都合を断ち切る。"""
        return User(
            id=UserId(row.id),
            email=Email(row.email),
            display_name=row.display_name,
            is_active=row.is_active,
        )
