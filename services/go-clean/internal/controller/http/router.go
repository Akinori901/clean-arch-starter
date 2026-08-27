// Package http は外部との接点。HTTP の語彙を扱ってよい唯一の層。
//
// controller がやってよいのは 3 つだけ:
//  1. 入力の検証
//  2. usecase の呼び出し
//  3. 応答の組み立て（ドメインエラー → HTTP ステータスの変換）
//
// ビジネスロジックを書かないこと。
package http

import (
	"encoding/json"
	"errors"
	"log/slog"
	"net/http"
	"strings"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/usecase"
	"github.com/go-chi/chi/v5"
)

// Router は HTTP ルーティングを組み立てる。
type Router struct {
	auth   *usecase.AuthUseCase
	health *usecase.HealthUseCase
}

// NewRouter は依存を注入して組み立てる。
func NewRouter(auth *usecase.AuthUseCase, health *usecase.HealthUseCase) *Router {
	return &Router{auth: auth, health: health}
}

// Handler はルータを返す。
//
// *chi.Mux をそのまま返すのは、Lambda アダプタ (aws-lambda-go-api-proxy) が
// 具象型を要求するため。http.Handler で包むと Lambda 側で使えない。
func (rt *Router) Handler() *chi.Mux {
	r := chi.NewRouter()
	r.Route("/api", func(r chi.Router) {
		r.Get("/health", rt.getHealth)
		r.Get("/health/live", rt.getLiveness)
		r.Post("/auth/sign-in", rt.postSignIn)
		r.Get("/auth/me", rt.getCurrentUser)
	})
	return r
}

type signInRequest struct {
	Email    string `json:"email"`
	Password string `json:"password"`
}

type userResponse struct {
	UserID      string `json:"user_id"`
	Email       string `json:"email"`
	DisplayName string `json:"display_name"`
	IsActive    bool   `json:"is_active"`
}

type signInResponse struct {
	AccessToken  string       `json:"access_token"`
	IDToken      string       `json:"id_token"`
	RefreshToken string       `json:"refresh_token"`
	ExpiresIn    int32        `json:"expires_in"`
	User         userResponse `json:"user"`
}

type componentResponse struct {
	Name   string `json:"name"`
	State  string `json:"state"`
	Detail string `json:"detail"`
}

type healthResponse struct {
	Healthy    bool                `json:"healthy"`
	Components []componentResponse `json:"components"`
}

func (rt *Router) postSignIn(w http.ResponseWriter, r *http.Request) {
	var req signInRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeError(w, http.StatusBadRequest, "リクエストの形式が不正です")
		return
	}
	if req.Email == "" || len(req.Password) < 8 {
		writeError(w, http.StatusBadRequest, "メールアドレスとパスワード(8文字以上)は必須です")
		return
	}

	out, err := rt.auth.SignIn(r.Context(), req.Email, req.Password)
	if err != nil {
		writeDomainError(w, err)
		return
	}

	writeJSON(w, http.StatusOK, signInResponse{
		AccessToken:  out.Tokens.AccessToken,
		IDToken:      out.Tokens.IDToken,
		RefreshToken: out.Tokens.RefreshToken,
		ExpiresIn:    out.Tokens.ExpiresIn,
		User:         toUserResponse(out.User),
	})
}

func (rt *Router) getCurrentUser(w http.ResponseWriter, r *http.Request) {
	token := bearerToken(r)
	if token == "" {
		writeError(w, http.StatusUnauthorized, "Authorization ヘッダがありません")
		return
	}

	user, err := rt.auth.CurrentUser(r.Context(), token)
	if err != nil {
		writeDomainError(w, err)
		return
	}
	writeJSON(w, http.StatusOK, toUserResponse(user))
}

func (rt *Router) getHealth(w http.ResponseWriter, r *http.Request) {
	status := rt.health.Check(r.Context())

	components := make([]componentResponse, 0, len(status.Components))
	for _, c := range status.Components {
		components = append(components, componentResponse{
			Name: c.Name, State: string(c.State), Detail: c.Detail,
		})
	}

	// 依存が落ちていれば 503 を返す。ALB はステータスコードで判定するため、
	// 本文が返せていても 200 にしないこと。
	code := http.StatusOK
	if !status.IsHealthy() {
		code = http.StatusServiceUnavailable
	}
	writeJSON(w, code, healthResponse{Healthy: status.IsHealthy(), Components: components})
}

// getLiveness はプロセスの生存のみを見る（依存を確認しない）。
func (rt *Router) getLiveness(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func toUserResponse(u entity.User) userResponse {
	return userResponse{
		UserID:      u.ID.String(),
		Email:       u.Email.String(),
		DisplayName: u.DisplayName.String(),
		IsActive:    u.IsActive,
	}
}

func bearerToken(r *http.Request) string {
	h := r.Header.Get("Authorization")
	if !strings.HasPrefix(h, "Bearer ") {
		return ""
	}
	return strings.TrimSpace(strings.TrimPrefix(h, "Bearer "))
}

// writeDomainError はドメインのエラーを HTTP ステータスへ翻訳する。
//
// **この変換を行ってよいのは controller だけ。**
// entity や usecase が 401 を知る必要はない。
func writeDomainError(w http.ResponseWriter, err error) {
	switch {
	case errors.Is(err, entity.ErrAuthFailed),
		errors.Is(err, entity.ErrUserDeactivated):
		writeError(w, http.StatusUnauthorized, err.Error())
	case errors.Is(err, entity.ErrUserNotFound):
		writeError(w, http.StatusNotFound, err.Error())
	case errors.Is(err, entity.ErrInvalidEmail),
		errors.Is(err, entity.ErrInvalidUserID),
		errors.Is(err, entity.ErrInvalidDisplayName):
		writeError(w, http.StatusBadRequest, err.Error())
	default:
		// 想定外は 500。**レスポンスには詳細を出さないが、ログには必ず残す。**
		// 両方伏せると、本番で原因が追えなくなる。
		slog.Error("想定外のエラー", "error", err)
		writeError(w, http.StatusInternalServerError, "内部エラーが発生しました")
	}
}

func writeError(w http.ResponseWriter, code int, detail string) {
	writeJSON(w, code, map[string]string{"detail": detail})
}

func writeJSON(w http.ResponseWriter, code int, body any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(body)
}
