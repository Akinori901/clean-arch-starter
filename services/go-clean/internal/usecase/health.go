package usecase

import (
	"context"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
)

// HealthUseCase は各依存の疎通を確認して集約する。
type HealthUseCase struct {
	probes []HealthProbe
}

// NewHealthUseCase は依存を注入して組み立てる。
func NewHealthUseCase(probes ...HealthProbe) *HealthUseCase {
	return &HealthUseCase{probes: probes}
}

// Check は全構成要素を確認する。
//
// 1つ落ちても残りの確認は続ける。全体像が見えないと切り分けができない。
func (uc *HealthUseCase) Check(ctx context.Context) entity.HealthStatus {
	var status entity.HealthStatus

	for _, p := range uc.probes {
		if err := p.Check(ctx); err != nil {
			detail := err.Error()
			if len(detail) > 200 {
				detail = detail[:200]
			}
			status.Add(entity.ComponentHealth{
				Name:   p.Name(),
				State:  entity.StateDown,
				Detail: detail,
			})
			continue
		}
		status.Add(entity.ComponentHealth{Name: p.Name(), State: entity.StateUp})
	}

	return status
}
