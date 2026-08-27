// Command app はエントリポイント。
//
// ローカルでは HTTP サーバとして、Lambda では Lambda ハンドラとして動く。
// 判定は AWS_LAMBDA_FUNCTION_NAME の有無で行い、**同じ Handler を共有する**。
// 実行環境ごとにルーティングを書き分けないこと。
package main

import (
	"context"
	"errors"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/app"
	"github.com/aws/aws-lambda-go/lambda"
	chiadapter "github.com/awslabs/aws-lambda-go-api-proxy/chi"
	"github.com/go-chi/chi/v5"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	slog.SetDefault(logger)

	cfg := app.LoadConfig()

	// 重い初期化はハンドラの外で済ませる。
	// Lambda では実行環境が再利用されるため、コールドスタート時の一度だけになる。
	handler, closeDB, err := app.Build(context.Background(), cfg)
	if err != nil {
		logger.Error("起動に失敗", "error", err)
		os.Exit(1)
	}
	defer func() { _ = closeDB() }()

	if os.Getenv("AWS_LAMBDA_FUNCTION_NAME") != "" {
		runLambda(handler)
		return
	}
	runServer(handler, cfg.Port, logger)
}

// runLambda は API Gateway HTTP API (payload v2) 経由で動かす。
//
// アダプタは *chi.Mux を要求するため、app.Build はその具象型を返している。
func runLambda(mux *chi.Mux) {
	lambda.Start(chiadapter.NewV2(mux).ProxyWithContextV2)
}

func runServer(handler http.Handler, port string, logger *slog.Logger) {
	srv := &http.Server{
		Addr:              ":" + port,
		Handler:           handler,
		ReadHeaderTimeout: 5 * time.Second,
	}

	go func() {
		logger.Info("サーバ起動", "port", port)
		if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			logger.Error("サーバが異常終了", "error", err)
			os.Exit(1)
		}
	}()

	// SIGTERM を受けたら処理中のリクエストを捌いてから落ちる
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt, syscall.SIGTERM)
	<-stop

	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	if err := srv.Shutdown(ctx); err != nil {
		logger.Error("graceful shutdown に失敗", "error", err)
	}
	logger.Info("停止しました")
}
