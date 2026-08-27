package usecase_test

import (
	"context"
	"errors"
	"testing"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/usecase"
)

type stubProbe struct {
	name string
	err  error
}

func (s stubProbe) Name() string                  { return s.name }
func (s stubProbe) Check(_ context.Context) error { return s.err }

func TestHealthAllUp(t *testing.T) {
	t.Parallel()

	uc := usecase.NewHealthUseCase(stubProbe{name: "database"}, stubProbe{name: "cognito"})
	status := uc.Check(context.Background())

	if !status.IsHealthy() {
		t.Error("全て up なのに unhealthy になっている")
	}
	if len(status.Components) != 2 {
		t.Errorf("Components = %d, want 2", len(status.Components))
	}
}

func TestHealthSingleDownMakesWholeUnhealthy(t *testing.T) {
	t.Parallel()

	uc := usecase.NewHealthUseCase(
		stubProbe{name: "database"},
		stubProbe{name: "cognito", err: errors.New("connection refused")},
	)
	status := uc.Check(context.Background())

	if status.IsHealthy() {
		t.Error("1つ落ちているのに healthy になっている")
	}

	degraded := status.Degraded()
	if len(degraded) != 1 || degraded[0].Name != "cognito" {
		t.Fatalf("Degraded() = %+v", degraded)
	}
	if degraded[0].Detail != "connection refused" {
		t.Errorf("Detail = %q", degraded[0].Detail)
	}
}

func TestHealthProbeFailureDoesNotAbortRemaining(t *testing.T) {
	t.Parallel()

	// 1つ落ちても他の確認は続ける。全体像が見えないと切り分けができない。
	uc := usecase.NewHealthUseCase(
		stubProbe{name: "a", err: errors.New("boom")},
		stubProbe{name: "b"},
		stubProbe{name: "c", err: errors.New("boom")},
	)
	status := uc.Check(context.Background())

	if len(status.Components) != 3 {
		t.Fatalf("Components = %d, want 3", len(status.Components))
	}
	want := []entity.ComponentState{entity.StateDown, entity.StateUp, entity.StateDown}
	for i, w := range want {
		if status.Components[i].State != w {
			t.Errorf("Components[%d].State = %q, want %q", i, status.Components[i].State, w)
		}
	}
}

func TestHealthEmptyIsHealthy(t *testing.T) {
	t.Parallel()

	// 確認対象が無い場合は健全とみなす
	if !usecase.NewHealthUseCase().Check(context.Background()).IsHealthy() {
		t.Error("確認対象ゼロが unhealthy になっている")
	}
}
