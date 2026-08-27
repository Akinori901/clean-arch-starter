// Package usecase はアプリケーション固有のビジネスルールを持つ。
//
// **依存の向きは内側（entity）のみ。**
// repo や controller を import してはならない。
//
// Go の慣習として、インターフェースは「使う側」で定義する。
// そのため Repository / 外部サービスの契約は repo パッケージではなく
// ここに置き、repo 側がそれを満たす（依存性逆転）。
package usecase

import (
	"context"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
)

// UserRepo はユーザーの永続化契約。
//
// 戻り値は必ず entity。*sql.Row や ORM の型を返さないこと。
// 返した瞬間に、永続化の都合が usecase より上へ漏れ出す。
type UserRepo interface {
	FindByID(ctx context.Context, id entity.UserID) (entity.User, error)
	FindByEmail(ctx context.Context, email entity.Email) (entity.User, error)
	Save(ctx context.Context, user entity.User) (entity.User, error)
}

// Authenticator は認証基盤の契約。
//
// Cognito という具体名をここに出さないこと。
// usecase は「認証できること」だけを知っていればよい。
type Authenticator interface {
	SignIn(ctx context.Context, email entity.Email, password string) (entity.AuthTokens, error)
	VerifyAccessToken(ctx context.Context, token string) (entity.VerifiedIdentity, error)
}

// HealthProbe は外部依存の死活確認の契約。
//
// 成否は error で表す。bool を返さないこと（理由が失われる）。
type HealthProbe interface {
	Name() string
	Check(ctx context.Context) error
}
