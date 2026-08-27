"""Email 値オブジェクト。

値オブジェクトは不変（frozen）で、等価性は「値」で決まる。
バリデーションはコンストラクタに置く。不正な Email は生成できない、
という状態を型で保証するのが値オブジェクトの役割。
"""
from __future__ import annotations

import re
from dataclasses import dataclass

from domain.exceptions import InvalidEmailError

_PATTERN = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


@dataclass(frozen=True)
class Email:
    value: str

    def __post_init__(self) -> None:
        if not _PATTERN.match(self.value):
            raise InvalidEmailError(f"メールアドレスの形式が不正です: {self.value}")

    def __str__(self) -> str:
        return self.value
