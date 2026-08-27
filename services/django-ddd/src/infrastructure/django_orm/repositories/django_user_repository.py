"""UserRepository の Django 実装。

Model を扱ってよい唯一の場所。
**返す直前に必ず Model → 集約へ変換する。**
Model をそのまま返すと、呼び出し元が ORM に依存してしまう。
"""
from __future__ import annotations

from domain.aggregates.profile import Profile
from domain.aggregates.user_account import UserAccount
from domain.entities.user import User
from domain.repositories.user_repository import UserRepository
from domain.value_objects.display_name import DisplayName
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId
from infrastructure.django_orm.models import UserModel


class DjangoUserRepository(UserRepository):
    def find_by_id(self, user_id: UserId) -> UserAccount | None:
        row = UserModel.objects.filter(pk=str(user_id)).first()
        return self._to_aggregate(row) if row else None

    def find_by_email(self, email: Email) -> UserAccount | None:
        row = UserModel.objects.filter(email=str(email)).first()
        return self._to_aggregate(row) if row else None

    def save(self, account: UserAccount) -> UserAccount:
        # 集約まるごと 1 回で保存する。内部エンティティだけの部分更新はしない。
        row, _ = UserModel.objects.update_or_create(
            pk=str(account.id),
            defaults={
                "email": str(account.user.email),
                "display_name": str(account.profile.display_name),
                "bio": account.profile.bio,
                "is_active": account.user.is_active,
            },
        )
        return self._to_aggregate(row)

    @staticmethod
    def _to_aggregate(row: UserModel) -> UserAccount:
        """Model → 集約への変換。この境界で ORM の都合を断ち切る。"""
        return UserAccount(
            user=User(
                id=UserId(row.id),
                email=Email(row.email),
                display_name=row.display_name,
                is_active=row.is_active,
            ),
            profile=Profile(
                display_name=DisplayName(row.display_name),
                # bio は他サービスと共有するため DB では null 許容。
                # ドメイン側は「未設定＝空文字」として扱う。
                bio=row.bio or "",
            ),
        )
