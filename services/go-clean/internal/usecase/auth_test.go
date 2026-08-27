package usecase_test

import (
	"context"
	"errors"
	"testing"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/usecase"
)

// 契約が interface になっていることの実利。
// Cognito も MySQL も無しでユースケースを完全に検証できる。

type fakeAuth struct {
	subject  string
	password string
}

func (f fakeAuth) SignIn(_ context.Context, _ entity.Email, password string) (entity.AuthTokens, error) {
	if password != f.password {
		return entity.AuthTokens{}, entity.ErrAuthFailed
	}
	return entity.AuthTokens{AccessToken: "access-" + f.subject, ExpiresIn: 3600}, nil
}

func (f fakeAuth) VerifyAccessToken(_ context.Context, token string) (entity.VerifiedIdentity, error) {
	if token != "access-"+f.subject {
		return entity.VerifiedIdentity{}, entity.ErrAuthFailed
	}
	return entity.VerifiedIdentity{Subject: f.subject, Email: "taro@example.com"}, nil
}

type inMemoryUsers struct {
	store map[string]entity.User
}

func newInMemoryUsers(seed ...entity.User) *inMemoryUsers {
	m := &inMemoryUsers{store: map[string]entity.User{}}
	for _, u := range seed {
		m.store[u.ID.String()] = u
	}
	return m
}

func (r *inMemoryUsers) FindByID(_ context.Context, id entity.UserID) (entity.User, error) {
	u, ok := r.store[id.String()]
	if !ok {
		return entity.User{}, entity.ErrUserNotFound
	}
	return u, nil
}

func (r *inMemoryUsers) FindByEmail(_ context.Context, email entity.Email) (entity.User, error) {
	for _, u := range r.store {
		if u.Email == email {
			return u, nil
		}
	}
	return entity.User{}, entity.ErrUserNotFound
}

func (r *inMemoryUsers) Save(_ context.Context, u entity.User) (entity.User, error) {
	r.store[u.ID.String()] = u
	return u, nil
}

func mustUser(t *testing.T, id, email string, active bool) entity.User {
	t.Helper()
	uid, err := entity.NewUserID(id)
	if err != nil {
		t.Fatalf("UserID: %v", err)
	}
	em, err := entity.NewEmail(email)
	if err != nil {
		t.Fatalf("Email: %v", err)
	}
	u, err := entity.NewUser(uid, em)
	if err != nil {
		t.Fatalf("User: %v", err)
	}
	u.IsActive = active
	return u
}

func TestSignInProvisionsLocalUserOnFirstSignIn(t *testing.T) {
	t.Parallel()

	users := newInMemoryUsers()
	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "correct-pass"}, users)

	out, err := uc.SignIn(context.Background(), "taro@example.com", "correct-pass")
	if err != nil {
		t.Fatalf("予期しないエラー: %v", err)
	}
	if out.User.ID.String() != "sub-1" {
		t.Errorf("UserID = %q", out.User.ID.String())
	}
	if out.User.DisplayName.String() != "taro" {
		t.Errorf("DisplayName = %q", out.User.DisplayName.String())
	}
	// Cognito 側にしか居なかったユーザーがローカルにも作られている
	if _, ok := users.store["sub-1"]; !ok {
		t.Error("ローカルにユーザーが作られていない")
	}
}

func TestSignInRejectsWrongPassword(t *testing.T) {
	t.Parallel()

	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "correct-pass"}, newInMemoryUsers())

	_, err := uc.SignIn(context.Background(), "taro@example.com", "wrong-pass")
	if !errors.Is(err, entity.ErrAuthFailed) {
		t.Errorf("err = %v, want ErrAuthFailed", err)
	}
}

func TestSignInRejectsDeactivatedUserEvenIfCognitoAccepts(t *testing.T) {
	t.Parallel()

	// 認証基盤の状態と、業務上の有効/無効は別の関心事である
	users := newInMemoryUsers(mustUser(t, "sub-1", "taro@example.com", false))
	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "correct-pass"}, users)

	_, err := uc.SignIn(context.Background(), "taro@example.com", "correct-pass")
	if !errors.Is(err, entity.ErrUserDeactivated) {
		t.Errorf("err = %v, want ErrUserDeactivated", err)
	}
}

func TestSignInRejectsInvalidEmailBeforeCallingAuth(t *testing.T) {
	t.Parallel()

	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "correct-pass"}, newInMemoryUsers())

	_, err := uc.SignIn(context.Background(), "not-an-email", "correct-pass")
	if !errors.Is(err, entity.ErrInvalidEmail) {
		t.Errorf("err = %v, want ErrInvalidEmail", err)
	}
}

func TestCurrentUserReturnsNotFoundWhenAbsent(t *testing.T) {
	t.Parallel()

	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "p"}, newInMemoryUsers())

	_, err := uc.CurrentUser(context.Background(), "access-sub-1")
	if !errors.Is(err, entity.ErrUserNotFound) {
		t.Errorf("err = %v, want ErrUserNotFound", err)
	}
}

func TestCurrentUserRejectsInvalidToken(t *testing.T) {
	t.Parallel()

	uc := usecase.NewAuthUseCase(fakeAuth{subject: "sub-1", password: "p"}, newInMemoryUsers())

	_, err := uc.CurrentUser(context.Background(), "garbage")
	if !errors.Is(err, entity.ErrAuthFailed) {
		t.Errorf("err = %v, want ErrAuthFailed", err)
	}
}
