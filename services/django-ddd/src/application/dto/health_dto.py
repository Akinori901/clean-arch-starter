"""ヘルスチェックの出力 DTO。"""
from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class ComponentOutput:
    name: str
    state: str
    detail: str


@dataclass(frozen=True)
class HealthOutput:
    healthy: bool
    components: list[ComponentOutput]
