package usecase

import (
	"context"
	"errors"
	"fmt"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
)

// SignInOutput はサインイン結果。
//
// entity をそのまま外へ返さず、出力専用の型に詰め替える。
// controller が entity の内部構造に依存するのを避けるため。
type SignInOutput struct {
	Tokens entity.AuthTokens
	User   entity.User
}

// AuthUseCase は認証のユースケース。
//
// 依存はすべてインターフェースで受け取り、コンストラクタで注入する。
// ここで具象型（Cognito クライアント等）を import しないこと。
type AuthUseCase struct {
	auth  Authenticator
	users UserRepo
}

// NewAuthUseCase は依存を注入して組み立てる。
func NewAuthUseCase(auth Authenticator, users UserRepo) *AuthUseCase {
	return &AuthUseCase{auth: auth, users: users}
}

// SignIn は認証し、ローカル側のユーザーを解決して返す。
func (uc *AuthUseCase) SignIn(ctx context.Context, rawEmail, password string) (SignInOutput, error) {
	email, err := entity.NewEmail(rawEmail)
	if err != nil {
		return SignInOutput{}, err
	}

	// 1. 認証基盤（Cognito）で認証する
	tokens, err := uc.auth.SignIn(ctx, email, password)
	if err != nil {
		return SignInOutput{}, err
	}

	// 2. 検証済みトークンから本人を特定する
	identity, err := uc.auth.VerifyAccessToken(ctx, tokens.AccessToken)
	if err != nil {
		return SignInOutput{}, err
	}

	// 3. ローカル側のユーザーを解決する（初回サインインなら作る）
	//    Cognito が正で、ローカルはプロフィールの保持のみを担う。
	user, err := uc.resolveUser(ctx, identity.Subject, email)
	if err != nil {
		return SignInOutput{}, err
	}

	// 4. 無効化されたアカウントは、Cognito 側が通しても拒否する。
	//    判定規則は entity が持つ。ここでは呼ぶだけ。
	if !user.CanSignIn() {
		return SignInOutput{}, entity.ErrUserDeactivated
	}

	return SignInOutput{Tokens: tokens, User: user}, nil
}

// CurrentUser はアクセストークンから現在のユーザーを返す。
func (uc *AuthUseCase) CurrentUser(ctx context.Context, accessToken string) (entity.User, error) {
	identity, err := uc.auth.VerifyAccessToken(ctx, accessToken)
	if err != nil {
		return entity.User{}, err
	}

	id, err := entity.NewUserID(identity.Subject)
	if err != nil {
		return entity.User{}, err
	}

	user, err := uc.users.FindByID(ctx, id)
	if err != nil {
		return entity.User{}, err
	}
	return user, nil
}

// resolveUser は既存ユーザーを引き、無ければ作る。
func (uc *AuthUseCase) resolveUser(ctx context.Context, subject string, email entity.Email) (entity.User, error) {
	id, err := entity.NewUserID(subject)
	if err != nil {
		return entity.User{}, err
	}

	user, err := uc.users.FindByID(ctx, id)
	switch {
	case err == nil:
		return user, nil
	case !errors.Is(err, entity.ErrUserNotFound):
		// 「見つからない」以外は本物の障害なので、そのまま返す
		return entity.User{}, fmt.Errorf("ユーザー取得に失敗: %w", err)
	}

	created, err := entity.NewUser(id, email)
	if err != nil {
		return entity.User{}, err
	}
	return uc.users.Save(ctx, created)
}
