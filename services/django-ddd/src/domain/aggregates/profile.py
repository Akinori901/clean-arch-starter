"""Profile — UserAccount 集約の**内部エンティティ**。

**集約の外から直接触ってはならない。**
変更は必ず集約ルート（UserAccount）のメソッドを経由すること。
直接書き換えると、ルートが守っている不変条件
（例: 無効なアカウントは変更できない）を迂回できてしまう。

独立したモジュールに置いてあるのは、import-linter が
「application / interfaces からこのモジュールを import していないか」を
検証できるようにするため（import-linter の判定単位はモジュール）。
"""
from __future__ import annotations

from dataclasses import dataclass

from domain.value_objects.display_name import DisplayName


@dataclass
class Profile:
    display_name: DisplayName
    bio: str = ""
