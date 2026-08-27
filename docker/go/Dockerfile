# syntax=docker/dockerfile:1

# ── ビルド ────────────────────────────────────────────────────
FROM golang:1.25 AS build
WORKDIR /src

# 依存だけ先に取ってレイヤキャッシュを効かせる
COPY go.mod go.sum* ./
RUN go mod download

COPY . .
# CGO を切って静的バイナリにする。Lambda のベースイメージには
# 期待する libc が無いことがあり、動的リンクだと起動しない。
RUN CGO_ENABLED=0 GOOS=linux go build -trimpath -ldflags="-s -w" -o /out/app ./cmd/app

# ── 開発用 ───────────────────────────────────────────────────
# ソースはマウントするので、ここでは go のツールチェーンだけ用意する。
FROM golang:1.25 AS dev
WORKDIR /app
ENV CGO_ENABLED=0
EXPOSE 8002

# ── 本番（Lambda コンテナイメージ）─────────────────────────────
# Go は自前で起動できるため、Lambda のカスタムランタイム(provided.al2023)を使う。
# cmd/app/main.go が AWS_LAMBDA_FUNCTION_NAME を見て Lambda モードへ切り替える。
FROM public.ecr.aws/lambda/provided:al2023 AS lambda
COPY --from=build /out/app ${LAMBDA_TASK_ROOT}/bootstrap
ENTRYPOINT ["/var/task/bootstrap"]
