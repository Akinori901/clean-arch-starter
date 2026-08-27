"""トランザクション境界の抽象。

application が django.db.transaction を直接呼ぶと Django 依存が生まれる。
UseCase は「まとめてコミットされること」だけを知ればよい。
"""
from __future__ import annotations

from abc import ABC, abstractmethod
from types import TracebackType


class UnitOfWork(ABC):
    @abstractmethod
    def __enter__(self) -> UnitOfWork:
        ...

    @abstractmethod
    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        ...
