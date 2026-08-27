"""認証ユースケースの入出力 DTO。

層をまたぐデータは必ず frozen dataclass にする。
呼び出し先で書き換わると、どこで変わったか追えなくなる。
"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class SignInInput:
    email: str
    password: str


@dataclass(frozen=True)
class SignInOutput:
    access_token: str
    id_token: str
    refresh_token: str
    expires_in: int
    user_id: str
    email: str
    display_name: str


@dataclass(frozen=True)
class CurrentUserOutput:
    user_id: str
    email: str
    display_name: str
    is_active: bool
