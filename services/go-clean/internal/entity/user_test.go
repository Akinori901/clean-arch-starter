package entity_test

import (
	"errors"
	"testing"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
)

// entity のテストは DB もフレームワークも使わずに動く。
// これが層を分けた実利。

func TestNewEmail(t *testing.T) {
	t.Parallel()

	t.Run("正しい形式を受け付ける", func(t *testing.T) {
		t.Parallel()
		e, err := entity.NewEmail("taro@example.com")
		if err != nil {
			t.Fatalf("予期しないエラー: %v", err)
		}
		if e.String() != "taro@example.com" {
			t.Errorf("String() = %q", e.String())
		}
	})

	for _, bad := range []string{"", "no-at-sign", "a@b", "a b@example.com"} {
		t.Run("不正な形式を弾く/"+bad, func(t *testing.T) {
			t.Parallel()
			if _, err := entity.NewEmail(bad); !errors.Is(err, entity.ErrInvalidEmail) {
				t.Errorf("err = %v, want ErrInvalidEmail", err)
			}
		})
	}
}

func TestEmailLocalPart(t *testing.T) {
	t.Parallel()

	e, err := entity.NewEmail("taro@example.com")
	if err != nil {
		t.Fatalf("予期しないエラー: %v", err)
	}
	if got := e.LocalPart(); got != "taro" {
		t.Errorf("LocalPart() = %q, want %q", got, "taro")
	}
}

func TestEmailEqualityIsByValue(t *testing.T) {
	t.Parallel()

	a, _ := entity.NewEmail("a@example.com")
	b, _ := entity.NewEmail("a@example.com")
	// 値オブジェクトなので比較可能で、値が同じなら等価
	if a != b {
		t.Error("同じ値の Email が等価でない")
	}
}

func TestNewUserID(t *testing.T) {
	t.Parallel()

	if _, err := entity.NewUserID("   "); !errors.Is(err, entity.ErrInvalidUserID) {
		t.Errorf("空白のみの ID が通ってしまった: %v", err)
	}
}

func TestNewDisplayName(t *testing.T) {
	t.Parallel()

	t.Run("上限ちょうどは通る", func(t *testing.T) {
		t.Parallel()
		name := make([]rune, 50)
		for i := range name {
			name[i] = 'あ'
		}
		if _, err := entity.NewDisplayName(string(name)); err != nil {
			t.Errorf("50文字が弾かれた: %v", err)
		}
	})

	t.Run("上限超過は弾く", func(t *testing.T) {
		t.Parallel()
		name := make([]rune, 51)
		for i := range name {
			name[i] = 'あ'
		}
		if _, err := entity.NewDisplayName(string(name)); !errors.Is(err, entity.ErrInvalidDisplayName) {
			t.Errorf("51文字が通ってしまった: %v", err)
		}
	})
}

func TestNewUserDerivesDisplayNameFromEmail(t *testing.T) {
	t.Parallel()

	id, _ := entity.NewUserID("sub-1")
	email, _ := entity.NewEmail("taro@example.com")

	user, err := entity.NewUser(id, email)
	if err != nil {
		t.Fatalf("予期しないエラー: %v", err)
	}
	if user.DisplayName.String() != "taro" {
		t.Errorf("DisplayName = %q, want %q", user.DisplayName.String(), "taro")
	}
	if !user.IsActive {
		t.Error("新規ユーザーが非アクティブになっている")
	}
}

func TestUserCanSignIn(t *testing.T) {
	t.Parallel()

	id, _ := entity.NewUserID("sub-1")
	email, _ := entity.NewEmail("taro@example.com")
	user, _ := entity.NewUser(id, email)

	if !user.CanSignIn() {
		t.Error("有効なユーザーがサインインできない")
	}

	user.Deactivate()
	if user.CanSignIn() {
		t.Error("無効化されたユーザーがサインインできてしまう")
	}
}

func TestUserEqualityIsByIdentityOnly(t *testing.T) {
	t.Parallel()

	id, _ := entity.NewUserID("same")
	emailA, _ := entity.NewEmail("a@example.com")
	emailB, _ := entity.NewEmail("b@example.com")

	a, _ := entity.NewUser(id, emailA)
	b, _ := entity.NewUser(id, emailB)

	// 値が違っても識別子が同じなら同じエンティティ
	if !a.Equals(b) {
		t.Error("同じ ID のエンティティが等価でない")
	}
}
