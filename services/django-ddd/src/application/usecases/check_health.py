"""ヘルスチェックユースケース。

各依存の疎通確認は Port の実装（infrastructure）が行う。
「1つでも落ちていたら unhealthy」という判定規則は
HealthStatus エンティティが持つ。ここでは並べて集約するだけ。
"""
from __future__ import annotations

from application.dto.health_dto import ComponentOutput, HealthOutput
from application.ports.health_probe import HealthProbePort
from domain.entities.health_status import ComponentHealth, ComponentState, HealthStatus


class CheckHealthUseCase:
    def __init__(self, probes: list[HealthProbePort]) -> None:
        self._probes = probes

    def execute(self) -> HealthOutput:
        status = HealthStatus()

        for probe in self._probes:
            try:
                probe.check()
                status.add(
                    ComponentHealth(name=probe.component_name, state=ComponentState.UP)
                )
            except Exception as exc:  # noqa: BLE001 - 疎通失敗の理由は問わず DOWN 扱い
                status.add(
                    ComponentHealth(
                        name=probe.component_name,
                        state=ComponentState.DOWN,
                        detail=str(exc)[:200],
                    )
                )

        return HealthOutput(
            healthy=status.is_healthy,
            components=[
                ComponentOutput(name=c.name, state=c.state.value, detail=c.detail)
                for c in status.components
            ],
        )
