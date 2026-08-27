"""DisplayName 値オブジェクト。"""
from __future__ import annotations

from dataclasses import dataclass

from domain.exceptions import InvalidDisplayNameError
from domain.value_objects.email import Email

_MAX_LENGTH = 50


@dataclass(frozen=True)
class DisplayName:
    value: str

    def __post_init__(self) -> None:
        if not self.value.strip():
            raise InvalidDisplayNameError("表示名が空です")
        if len(self.value) > _MAX_LENGTH:
            raise InvalidDisplayNameError(
                f"表示名は{_MAX_LENGTH}文字以内にしてください"
            )

    @classmethod
    def from_email(cls, email: Email) -> DisplayName:
        """メールアドレスのローカル部を既定の表示名にする。"""
        return cls(str(email).split("@")[0])

    def __str__(self) -> str:
        return self.value
