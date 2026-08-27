package entity

// ComponentState は構成要素の状態。
type ComponentState string

const (
	StateUp   ComponentState = "up"
	StateDown ComponentState = "down"
)

// ComponentHealth は個々の依存の状態。
type ComponentHealth struct {
	Name   string
	State  ComponentState
	Detail string
}

// HealthStatus はヘルスチェック全体の結果。
//
// 「1つでも落ちていたら unhealthy」という判定規則はドメインの知識なので、
// controller 側で if を並べずにここへ置く。
type HealthStatus struct {
	Components []ComponentHealth
}

// Add は構成要素の結果を追加する。
func (h *HealthStatus) Add(c ComponentHealth) {
	h.Components = append(h.Components, c)
}

// IsHealthy は全構成要素が Up のときのみ true を返す。
func (h HealthStatus) IsHealthy() bool {
	for _, c := range h.Components {
		if c.State != StateUp {
			return false
		}
	}
	return true
}

// Degraded は落ちている構成要素だけを返す。
func (h HealthStatus) Degraded() []ComponentHealth {
	var out []ComponentHealth
	for _, c := range h.Components {
		if c.State != StateUp {
			out = append(out, c)
		}
	}
	return out
}
