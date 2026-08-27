"""HealthStatus エンティティ。

ヘルスチェックの結果を表す。「どの依存が落ちているか」の判定規則は
ドメインの知識なので、ここに置く。View 側で if を並べない。
"""
from __future__ import annotations

from dataclasses import dataclass, field
from enum import StrEnum


class ComponentState(StrEnum):
    UP = "up"
    DOWN = "down"


@dataclass(frozen=True)
class ComponentHealth:
    name: str
    state: ComponentState
    detail: str = ""


@dataclass
class HealthStatus:
    components: list[ComponentHealth] = field(default_factory=list)

    def add(self, component: ComponentHealth) -> None:
        self.components.append(component)

    @property
    def is_healthy(self) -> bool:
        """全構成要素が UP のときのみ健全とみなす。"""
        return all(c.state is ComponentState.UP for c in self.components)

    @property
    def degraded_components(self) -> list[ComponentHealth]:
        return [c for c in self.components if c.state is ComponentState.DOWN]
