"""UserAccount 集約（Aggregate）。

DDD の中核概念。**集約は「不変条件を守る単位」であり、トランザクションの境界**でもある。

- 集約の外からは **集約ルート（Aggregate Root）経由でしか触れない。**
  ここでは UserAccount がルートで、内部の Profile は直接触らせない。
- 永続化の単位も集約ごと。**Repository は集約ルート単位で 1 つ**作る
  （Profile 用の Repository は作らない）。
- 集約をまたぐ整合性は「結果整合」で扱う。1トランザクションに複数集約を入れない。
"""
from __future__ import annotations

from dataclasses import dataclass, field

from domain.aggregates.profile import Profile
from domain.entities.user import User
from domain.exceptions import ProfileRuleViolationError
from domain.value_objects.display_name import DisplayName
from domain.value_objects.email import Email
from domain.value_objects.user_id import UserId

_MAX_BIO_LENGTH = 500


@dataclass
class UserAccount:
    """集約ルート。

    「アカウントとして成立しているか」の不変条件をここで守る。
    """

    user: User
    profile: Profile
    # 集約が起こした出来事。UseCase 側で拾って通知等に使う（ドメインイベント）。
    events: list[str] = field(default_factory=list)

    @classmethod
    def register(cls, user_id: UserId, email: Email) -> UserAccount:
        """新規登録のファクトリ。

        集約の生成もルートの責務。バラバラに new させない。
        """
        account = cls(
            user=User(id=user_id, email=email, display_name=""),
            profile=Profile(display_name=DisplayName.from_email(email)),
        )
        # display_name は Profile が正。User 側へ写して整合させる
        account.user.display_name = str(account.profile.display_name)
        account.events.append("UserRegistered")
        return account

    # ── 不変条件を伴う操作（すべてルート経由）──────────────────

    def rename(self, new_name: DisplayName) -> None:
        """表示名を変更する。"""
        if not self.user.can_sign_in():
            # 無効なアカウントは変更させない、というのが集約の不変条件
            raise ProfileRuleViolationError("無効なアカウントは変更できません")

        self.profile.display_name = new_name
        self.user.display_name = str(new_name)
        self.events.append("UserRenamed")

    def update_bio(self, bio: str) -> None:
        if len(bio) > _MAX_BIO_LENGTH:
            raise ProfileRuleViolationError(
                f"自己紹介は{_MAX_BIO_LENGTH}文字以内にしてください"
            )
        self.profile.bio = bio

    def deactivate(self) -> None:
        self.user.deactivate()
        self.events.append("UserDeactivated")

    def pull_events(self) -> list[str]:
        """溜まったイベントを取り出して空にする（二重発行を防ぐ）。"""
        drained, self.events = self.events, []
        return drained

    @property
    def id(self) -> UserId:
        return self.user.id
