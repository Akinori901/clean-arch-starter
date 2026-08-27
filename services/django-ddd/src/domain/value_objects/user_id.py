"""UserId 値オブジェクト。

Cognito の sub（UUID）をそのまま識別子として扱う。
プリミティブな str を持ち回すと、どの ID なのかが型から失われるため包む。
"""
from __future__ import annotations

from dataclasses import dataclass

from domain.exceptions import InvalidUserIdError


@dataclass(frozen=True)
class UserId:
    value: str

    def __post_init__(self) -> None:
        if not self.value or not self.value.strip():
            raise InvalidUserIdError("ユーザーIDが空です")

    def __str__(self) -> str:
        return self.value
