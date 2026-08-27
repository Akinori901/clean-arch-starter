// Package repo は永続化の実装。usecase が定義した契約を満たす。
//
// **SQL / ドライバを知ってよいのはこの層だけ。**
// 返す直前に必ず DB の行 → entity へ変換する。
package repo

import (
	"context"
	"database/sql"
	"errors"
	"fmt"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
)

// UserRepo は usecase.UserRepo の MySQL 実装。
type UserRepo struct {
	db *sql.DB
}

// NewUserRepo は依存を注入して組み立てる。
func NewUserRepo(db *sql.DB) *UserRepo {
	return &UserRepo{db: db}
}

const selectUserColumns = `SELECT id, email, display_name, is_active FROM users`

// FindByID は ID でユーザーを取得する。
//
// 見つからない場合は entity.ErrUserNotFound を返す。
// sql.ErrNoRows をそのまま上へ返さないこと。database/sql の型が漏れる。
func (r *UserRepo) FindByID(ctx context.Context, id entity.UserID) (entity.User, error) {
	row := r.db.QueryRowContext(ctx, selectUserColumns+` WHERE id = ?`, id.String())
	return r.scan(row)
}

// FindByEmail は Email でユーザーを取得する。
func (r *UserRepo) FindByEmail(ctx context.Context, email entity.Email) (entity.User, error) {
	row := r.db.QueryRowContext(ctx, selectUserColumns+` WHERE email = ?`, email.String())
	return r.scan(row)
}

// Save はユーザーを永続化する（新規・更新の両方）。
func (r *UserRepo) Save(ctx context.Context, user entity.User) (entity.User, error) {
	const q = `
		INSERT INTO users (id, email, display_name, is_active)
		VALUES (?, ?, ?, ?)
		ON DUPLICATE KEY UPDATE
			email = VALUES(email),
			display_name = VALUES(display_name),
			is_active = VALUES(is_active)`

	if _, err := r.db.ExecContext(ctx, q,
		user.ID.String(), user.Email.String(), user.DisplayName.String(), user.IsActive,
	); err != nil {
		return entity.User{}, fmt.Errorf("ユーザー保存に失敗: %w", err)
	}
	return user, nil
}

// scan は DB の行を entity へ変換する。この境界で永続化の都合を断ち切る。
func (r *UserRepo) scan(row *sql.Row) (entity.User, error) {
	var (
		rawID, rawEmail, rawName string
		isActive                 bool
	)
	if err := row.Scan(&rawID, &rawEmail, &rawName, &isActive); err != nil {
		if errors.Is(err, sql.ErrNoRows) {
			return entity.User{}, entity.ErrUserNotFound
		}
		return entity.User{}, fmt.Errorf("ユーザー取得に失敗: %w", err)
	}

	id, err := entity.NewUserID(rawID)
	if err != nil {
		return entity.User{}, err
	}
	email, err := entity.NewEmail(rawEmail)
	if err != nil {
		return entity.User{}, err
	}
	name, err := entity.NewDisplayName(rawName)
	if err != nil {
		return entity.User{}, err
	}

	return entity.User{ID: id, Email: email, DisplayName: name, IsActive: isActive}, nil
}
