from __future__ import annotations

from domain.entities.health_status import (
    ComponentHealth,
    ComponentState,
    HealthStatus,
)


def test_all_up_is_healthy() -> None:
    status = HealthStatus()
    status.add(ComponentHealth("database", ComponentState.UP))
    status.add(ComponentHealth("cognito", ComponentState.UP))

    assert status.is_healthy is True
    assert status.degraded_components == []


def test_single_down_makes_whole_unhealthy() -> None:
    status = HealthStatus()
    status.add(ComponentHealth("database", ComponentState.UP))
    status.add(ComponentHealth("cognito", ComponentState.DOWN, "timeout"))

    assert status.is_healthy is False
    assert [c.name for c in status.degraded_components] == ["cognito"]


def test_empty_status_is_healthy() -> None:
    # 確認対象が無い場合は健全とみなす（all([]) is True）
    assert HealthStatus().is_healthy is True
