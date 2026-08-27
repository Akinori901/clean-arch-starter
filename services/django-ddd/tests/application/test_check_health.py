from __future__ import annotations

from application.usecases.check_health import CheckHealthUseCase
from tests.application.fakes import StubProbe


def test_all_probes_up() -> None:
    usecase = CheckHealthUseCase([StubProbe("database"), StubProbe("cognito")])
    output = usecase.execute()

    assert output.healthy is True
    assert {c.name for c in output.components} == {"database", "cognito"}


def test_one_probe_down_reports_unhealthy_with_detail() -> None:
    usecase = CheckHealthUseCase(
        [StubProbe("database"), StubProbe("cognito", fails=True)]
    )
    output = usecase.execute()

    assert output.healthy is False
    down = next(c for c in output.components if c.name == "cognito")
    assert down.state == "down"
    assert "connection refused" in down.detail


def test_probe_failure_does_not_abort_remaining_probes() -> None:
    """1つ落ちても他の確認は続ける。全体像が見えないと切り分けできない。"""
    usecase = CheckHealthUseCase(
        [StubProbe("a", fails=True), StubProbe("b"), StubProbe("c", fails=True)]
    )
    output = usecase.execute()

    assert len(output.components) == 3
    assert [c.state for c in output.components] == ["down", "up", "down"]
