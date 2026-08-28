# syntax=docker/dockerfile:1

# ── ビルド ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 依存だけ先に取ってレイヤキャッシュを効かせる。
# .csproj だけを先に COPY するのは、ソース変更のたびに
# NuGet の復元をやり直さないため。
COPY DotnetClean.slnx global.json ./
COPY src/Domain/Domain.csproj                         src/Domain/
COPY src/Application/Application.csproj               src/Application/
COPY src/Infrastructure/Infrastructure.csproj         src/Infrastructure/
COPY src/Web/Web.csproj                               src/Web/
COPY tests/Domain.UnitTests/Domain.UnitTests.csproj           tests/Domain.UnitTests/
COPY tests/Application.UnitTests/Application.UnitTests.csproj tests/Application.UnitTests/
COPY tests/ArchitectureTests/ArchitectureTests.csproj         tests/ArchitectureTests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/Web/Web.csproj -c Release -o /out --no-restore

# ── 開発用 ───────────────────────────────────────────────────
# ソースはマウントするので、ここでは SDK だけ用意する。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
WORKDIR /app
# 起動のたびに「ようこそ」メッセージと初回テレメトリ通知を出さない
ENV DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    ASPNETCORE_URLS=http://0.0.0.0:8004
EXPOSE 8004

# ── 本番（Lambda コンテナイメージ）─────────────────────────────
# .NET は自前で起動できるため、Lambda のカスタムランタイム(provided.al2023)を使う。
# Program.cs の AddAWSLambdaHosting が AWS_LAMBDA_FUNCTION_NAME を見て
# Lambda モードへ切り替える（無ければ通常の Kestrel として動く）。
FROM public.ecr.aws/lambda/provided:al2023 AS lambda
COPY --from=build /out ${LAMBDA_TASK_ROOT}/
# Lambda は /tmp 以外書き込み不可。
# ASP.NET Core はデータ保護キーの保存先を必要とするため、そこへ逃がす。
ENV TMPDIR=/tmp \
    DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp \
    ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["/var/task/Web"]
