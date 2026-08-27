"""UnitOfWork の Django 実装。django.db.transaction をここに閉じ込める。"""
from __future__ import annotations

from types import TracebackType

from django.db import transaction

from application.ports.unit_of_work import UnitOfWork


class DjangoUnitOfWork(UnitOfWork):
    def __enter__(self) -> DjangoUnitOfWork:
        self._atomic = transaction.atomic()
        self._atomic.__enter__()
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc: BaseException | None,
        tb: TracebackType | None,
    ) -> None:
        self._atomic.__exit__(exc_type, exc, tb)
