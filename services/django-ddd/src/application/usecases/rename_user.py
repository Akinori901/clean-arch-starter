"""表示名変更ユースケース。

**集約とドメインサービスの使い分けを示す例。**

- 「無効なアカウントは変更できない」→ 集約 1 つで判定できる → 集約のメソッド
- 「メールアドレスが重複していないか」→ 他の集約との関係で決まる → ドメインサービス

ユースケースは、この 2 つを呼び分けて手順を組み立てるだけ。
"""
from __future__ import annotations

from application.dto.auth_dto import CurrentUserOutput
from application.ports.unit_of_work import UnitOfWork
from domain.exceptions import UserNotFoundError
from domain.repositories.user_repository import UserRepository
from domain.services.email_uniqueness_service import EmailUniquenessService
from domain.value_objects.display_name import DisplayName
from domain.value_objects.user_id import UserId


class RenameUserUseCase:
    def __init__(
        self,
        user_repository: UserRepository,
        email_uniqueness: EmailUniquenessService,
        unit_of_work: UnitOfWork,
    ) -> None:
        self._users = user_repository
        self._email_uniqueness = email_uniqueness
        self._uow = unit_of_work

    def execute(self, user_id: str, new_display_name: str) -> CurrentUserOutput:
        # 値オブジェクトの生成時点で表示名の妥当性が検証される
        name = DisplayName(new_display_name)

        # 集約はトランザクションの境界。1トランザクションに複数集約を入れない。
        with self._uow:
            account = self._users.find_by_id(UserId(user_id))
            if account is None:
                raise UserNotFoundError("ユーザーが見つかりません")

            # 単一集約で判定できる規則は集約が持つ
            account.rename(name)

            saved = self._users.save(account)

        # 集約が起こした出来事は、保存後に取り出して通知等へ回す
        _events = account.pull_events()

        return CurrentUserOutput(
            user_id=str(saved.id),
            email=str(saved.user.email),
            display_name=str(saved.profile.display_name),
            is_active=saved.user.is_active,
        )
