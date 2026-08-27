// Package app は依存の組立点（composition root）。
//
// **具象型を結線してよいのはここだけ。**
// controller や usecase が具象を直接組み立てると、層の境界が意味を失う。
//
// DI ライブラリ（wire / fx）は使っていない。この規模なら関数で十分で、
// 依存関係が読んで分かる方が価値が高い。
package app

import (
	"context"
	"database/sql"
	"fmt"
	"os"
	"time"

	nethttp "github.com/Akinori901/clean-arch-starter/services/go-clean/internal/controller/http"
	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/repo"
	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/usecase"
	"github.com/aws/aws-sdk-go-v2/aws"
	awsconfig "github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/service/cognitoidentityprovider"
	"github.com/aws/aws-sdk-go-v2/service/s3"
	"github.com/go-chi/chi/v5"
	"github.com/go-sql-driver/mysql"
)

// Config は環境変数から読む設定。
//
// 環境変数の読み取りをここへ集約する。各層が os.Getenv を直接読むと、
// 「何を設定すれば動くのか」がコード全体に散らばって追えなくなる。
type Config struct {
	DBHost     string
	DBPort     string
	DBName     string
	DBUser     string
	DBPassword string

	AWSRegion       string
	S3Bucket        string
	S3Endpoint      string
	CognitoEndpoint string
	CognitoPoolID   string
	CognitoClientID string
	CognitoSecret   string
	CognitoIssuer   string
	CognitoJWKSURL  string

	Port string
}

// LoadConfig は環境変数から設定を読む。
func LoadConfig() Config {
	return Config{
		DBHost:     env("DB_HOST", "127.0.0.1"),
		DBPort:     env("DB_PORT", "3306"),
		DBName:     env("DB_NAME", "app"),
		DBUser:     env("DB_USER", "app"),
		DBPassword: env("DB_PASSWORD", "app"),

		AWSRegion:       env("AWS_REGION", "ap-northeast-1"),
		S3Bucket:        env("S3_BUCKET", "app-static"),
		S3Endpoint:      env("S3_ENDPOINT_URL", ""),
		CognitoEndpoint: env("COGNITO_ENDPOINT_URL", ""),
		CognitoPoolID:   env("COGNITO_USER_POOL_ID", ""),
		CognitoClientID: env("COGNITO_CLIENT_ID", ""),
		CognitoSecret:   env("COGNITO_CLIENT_SECRET", ""),
		CognitoIssuer:   env("COGNITO_ISSUER_OVERRIDE", ""),
		CognitoJWKSURL:  env("COGNITO_JWKS_URL_OVERRIDE", ""),

		Port: env("PORT", "8002"),
	}
}

func env(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

// Build は全依存を結線してルータを返す。
//
// 返す closer は DB コネクションの解放に使う。
func Build(ctx context.Context, cfg Config) (*chi.Mux, func() error, error) {
	db, err := openDB(cfg)
	if err != nil {
		return nil, nil, err
	}

	awsCfg, err := awsconfig.LoadDefaultConfig(ctx, awsconfig.WithRegion(cfg.AWSRegion))
	if err != nil {
		_ = db.Close()
		return nil, nil, fmt.Errorf("AWS 設定の読み込みに失敗: %w", err)
	}

	// endpoint はローカル（SeaweedFS / cognito-local）のときだけ設定する。
	// 本番では空にして AWS の既定エンドポイントを使う。
	s3Client := s3.NewFromConfig(awsCfg, func(o *s3.Options) {
		if cfg.S3Endpoint != "" {
			o.BaseEndpoint = aws.String(cfg.S3Endpoint)
			// SeaweedFS 等の S3 互換実装は仮想ホスト形式に対応しないことがある
			o.UsePathStyle = true
		}
	})

	cognitoClient := cognitoidentityprovider.NewFromConfig(awsCfg, func(o *cognitoidentityprovider.Options) {
		if cfg.CognitoEndpoint != "" {
			o.BaseEndpoint = aws.String(cfg.CognitoEndpoint)
		}
	})

	// 契約 → 実装の結線。usecase 側は具象を知らない。
	users := repo.NewUserRepo(db)
	auth := repo.NewCognitoAuth(cognitoClient, repo.CognitoConfig{
		UserPoolID:      cfg.CognitoPoolID,
		ClientID:        cfg.CognitoClientID,
		ClientSecret:    cfg.CognitoSecret,
		Region:          cfg.AWSRegion,
		IssuerOverride:  cfg.CognitoIssuer,
		JWKSURLOverride: cfg.CognitoJWKSURL,
	})

	authUC := usecase.NewAuthUseCase(auth, users)
	healthUC := usecase.NewHealthUseCase(
		repo.NewDBProbe(db),
		repo.NewStorageProbe(s3Client, cfg.S3Bucket),
		repo.NewCognitoProbe(cognitoClient, cfg.CognitoPoolID),
	)

	return nethttp.NewRouter(authUC, healthUC).Handler(), db.Close, nil
}

func openDB(cfg Config) (*sql.DB, error) {
	dsn := (&mysql.Config{
		User:                 cfg.DBUser,
		Passwd:               cfg.DBPassword,
		Net:                  "tcp",
		Addr:                 cfg.DBHost + ":" + cfg.DBPort,
		DBName:               cfg.DBName,
		ParseTime:            true,
		AllowNativePasswords: true,
		Params:               map[string]string{"charset": "utf8mb4"},
	}).FormatDSN()

	db, err := sql.Open("mysql", dsn)
	if err != nil {
		return nil, fmt.Errorf("DB への接続に失敗: %w", err)
	}

	// Lambda では実行環境が再利用されるため、コネクションを絞って
	// 使い回す。RDS Proxy を挟む場合はさらに小さくしてよい。
	db.SetMaxOpenConns(5)
	db.SetMaxIdleConns(2)
	db.SetConnMaxLifetime(5 * time.Minute)

	return db, nil
}
