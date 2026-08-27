"""ドメイン例外。

HTTP ステータスコードをここに持ち込まないこと。
「認証に失敗した」はドメインの語彙だが、「401」は interfaces 層の語彙である。
変換は interfaces/api/views.py が行う。
"""
from __future__ import annotations


class DomainError(Exception):
    """全ドメイン例外の基底。"""


class InvalidEmailError(DomainError):
    pass


class InvalidUserIdError(DomainError):
    pass


class InvalidDisplayNameError(DomainError):
    pass


class ProfileRuleViolationError(DomainError):
    """集約の不変条件に反する操作。"""


class AuthenticationFailedError(DomainError):
    """認証情報が正しくない。"""


class UserNotFoundError(DomainError):
    pass


class EmailAlreadyTakenError(DomainError):
    """メールアドレスが既に他のユーザーに使われている。"""
