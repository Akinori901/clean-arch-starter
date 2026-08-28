# 70. C#(.NET) — クリーンアーキテクチャ構成ルール

対象: `services/dotnet-clean/`
検証: **ProjectReference（コンパイル時）** + `NetArchTest`（`tests/ArchitectureTests/`）

## なぜクリーンアーキテクチャか

.NET コミュニティではクリーンアーキテクチャが事実上の標準。
参照実装も **Jason Taylor 版（20.5k★）**・**Ardalis 版（18.4k★）** とクリーンアーキ寄りで、
どちらも **プロジェクト分割で依存方向を強制する**構成を採る。

**C# 最大の強みは、依存の向きをビルドシステムそのものが保証すること。**
`.csproj` の `ProjectReference` に無いプロジェクトの型は、そもそも**書けない**。
他言語のように「lint が後から怒る」のではなく、コンパイルが通らない。

## 層構成

```
Web/              ← エントリポイント・DI 組立。すべてを参照してよい唯一の場所
    ↓
Infrastructure/   ← Application の契約を実装。EF Core / AWS SDK はここだけ
    ↓
Application/      ← ユースケース + 契約(interface)の定義。Domain のみ参照
    ↓
Domain/           ← 最内層。**ProjectReference も PackageReference も持たない**
```

## ディレクトリと責務

```
services/dotnet-clean/
├── src/
│   ├── Domain/              # 依存ゼロ。BCL のみ
│   │   ├── ValueObjects/    # 不変・等価性は値（Email / UserId / DisplayName）
│   │   ├── Entities/        # 同一性を持つ（User / HealthStatus）
│   │   └── DomainException.cs   # ドメイン例外（HTTPステータスを持ち込まない）
│   ├── Application/
│   │   ├── Abstractions/    # 契約(interface)。実装は置かない
│   │   ├── UseCases/        # 1ファイル1ユースケース。公開は ExecuteAsync のみ
│   │   └── Dto/             # 層をまたぐデータ
│   ├── Infrastructure/
│   │   ├── Persistence/     # DbContext / Repository（EF Core はここだけ）
│   │   ├── Cognito/         # 認証基盤の実装
│   │   ├── Health/          # 各依存の疎通確認
│   │   └── DependencyInjection.cs   # 結線口。ここだけ public
│   └── Web/
│       ├── Endpoints/       # Minimal API。HTTP の語彙を扱ってよい唯一の層
│       ├── Contracts/       # リクエスト/レスポンス形（JSON キーは他スタックと共通）
│       └── Program.cs       # composition root
└── tests/
    ├── Domain.UnitTests/        # Domain のみ参照（DB も AWS も要らない）
    ├── Application.UnitTests/   # Fake で契約を満たす。Infrastructure を参照しない
    └── ArchitectureTests/       # 層依存の検証（NetArchTest）
```

## 依存ルール（ProjectReference が強制）

| 層 | 参照してよいプロジェクト | 外部パッケージ |
|---|---|---|
| `Domain` | **なし** | **なし**（BCL のみ） |
| `Application` | `Domain` | **なし** |
| `Infrastructure` | `Application`, `Domain` | EF Core / AWS SDK / JWT |
| `Web` | すべて | ASP.NET Core / Lambda ホスティング |

**この表は `.csproj` の `ProjectReference` と 1 対 1 で対応する。**
表を変えるときは `.csproj` も同時に変えること。

### C# 特有の作法

- **契約(interface)は「使う側」である `Application` に置く。**
  `Infrastructure` がそれを実装する。矢印は Infrastructure → Application、
  つまり外から内を向く（依存性逆転）。
- **`Infrastructure` の実装クラスは `internal` にする。**
  `public` にすると `Web` から直接 `new` できてしまい、
  「Web が触れるのは Application の契約だけ」という前提が崩れる。
  公開するのは結線口（`DependencyInjection`）と `Options` だけ。
  （ArchitectureTests がこれを検証する）
- **エンティティは `record` ではなく `class`。**
  `record` は値等価性を既定にするが、エンティティの等価性は識別子だけで決まる。
  `record` にすると「表示名を変えたら別人」という誤った等価性になる。
  逆に**値オブジェクトは `readonly record struct`** が最適
  （不変性と値等価性を言語機能で得られる）。
- **`TreatWarningsAsErrors` を有効にしてある。** 警告を放置すると
  「いつもの赤」になって誰も読まなくなる。

### 禁止事項（違反は CI で落ちる）

- ❌ `Domain` に `ProjectReference` / `PackageReference` を足す
- ❌ `Application` から `Infrastructure` を参照する（循環参照でビルドが落ちる）
- ❌ `Domain` / `Application` で EF Core・AWS SDK・ASP.NET Core の型を使う
- ❌ `Domain` に HTTP の語彙（`StatusCode` 等）を持ち込む
- ❌ `UserRecord`（DB の行）を `Infrastructure` の外へ出す
- ❌ **EF Core のマイグレーションを作る**（後述）

## 検証は 2 段構え

**ProjectReference だけでは足りない。** 以下はコンパイルを通ってしまう:

- `Domain.csproj` に `PackageReference` で EF Core を足す
- そのうえで `Domain` のエンティティが `DbContext` を持つ

`ProjectReference` は「他プロジェクトへの依存」しか縛らないため、
**NuGet パッケージ経由の層破壊は素通りする**。これを `NetArchTest` で落とす。

| 検証 | 何を守るか | 落ち方 |
|---|---|---|
| ProjectReference | プロジェクト間の依存方向 | `dotnet build` がコンパイルエラー |
| NetArchTest | パッケージ依存・HTTP 語彙の混入・実装クラスの公開 | `dotnet test` が失敗 |

どちらも**違反を注入したら実際に落ちることを確認済み**。

## users テーブルは共有物（重要）

`users` は **Django が所有**し、既存 4 スタックと**同じ行を共有する**。

- **EF Core のマイグレーションを作らないこと。**
  同じテーブルを 2 つのマイグレーション履歴が管理することになり、必ず壊れる。
  EF Core は「既存テーブルへマップするだけ」に徹する。
- カラム名は `HasColumnName` で **snake_case を明示**する
  （EF Core の既定は PascalCase のままで、列が見つからない）。
- `created_at` / `updated_at` は **DB 側の DEFAULT で入る**ため
  `ValueGeneratedOnAdd()` を付ける。付けないと既定値の `0001-01-01` を
  送ってしまい、MySQL が範囲外で拒否する。

## エラーの扱い

- ドメインの例外は `Domain/DomainException.cs` に定義する。
- HTTP ステータスへの変換は **`Web` の `DomainExceptionHandler` だけ**が行う。
  各エンドポイントで `try/catch` を書くと変換規則が散らばる。
- **認証失敗時、「ユーザーが存在しない」と「パスワードが違う」を区別しない。**
  区別するとアカウント列挙に使われる。既存 5 スタックすべてで同一メッセージ。
- **500 を返すときも、ログには必ず原因を残す。** 両方伏せると本番で追えない。

## ハマりどころ（実際に踏んだもの）

- **`ServiceURL` を設定すると `RegionEndpoint` が `null` になる。**
  代入した直後に `config.RegionEndpoint` を読むと `NullReferenceException`。
  region は変数に控えてから使うこと（ヘルスチェックが 500 になった）。
- **cognito-local は `iss` に `localhost:9229` を刻むが、
  コンテナからは `cognito:9229` でしか到達できない。**
  `COGNITO_ISSUER_OVERRIDE` と `COGNITO_JWKS_URL_OVERRIDE` を**別々に**指定する。
- **`ConfigurationManager<OpenIdConnectConfiguration>` は使えない。**
  あれは OIDC ディスカバリ文書（`.well-known/openid-configuration`）を前提にするが、
  cognito-local が公開するのは JWKS そのもの。
  素直に JWKS を取って `JsonWebKeySet.Create()` で解釈する。
- **cognito-local は `ExpiresIn` を返さないことがある。** 既定値へフォールバックする。
  ここで落とすと「本番でだけ動く」実装になる。
- **Cognito のアクセストークンには `aud` が無い**（代わりに `client_id`）。
  `ValidateAudience = true` のままだと必ず失敗する。`client_id` を明示的に照合する。
- **AWS SDK の例外は型で判定しないこと。**
  実 Cognito は型付き例外を返すが、cognito-local では汎用例外になり、
  型で見ると取りこぼして 500 になる。**`ErrorCode` 文字列**で判定する。
- **`FallbackCredentialsFactory` は AWS SDK v4 で廃止された。**
  `DefaultAWSCredentialsIdentityResolver`（`Amazon.Runtime.Credentials`）を使う。
- **xunit v3 は `Xunit` 名前空間の暗黙 using を持たない。**
  `GlobalUsings.cs` で `global using Xunit;` を宣言する。
- **.NET 10 は VSTest 経由のテスト実行を廃止した。**
  `global.json` で Microsoft.Testing.Platform に切り替える。
  無いと `dotnet test` が
  "Testing with VSTest target is no longer supported" で落ちる。
- **`TestResult` は NetArchTest と Xunit の両方にある。** 明示的に修飾する。

## Lambda での注意

- `provided.al2023` + `dotnet publish` の成果物。
- `AddAWSLambdaHosting()` は `AWS_LAMBDA_FUNCTION_NAME` が無ければ**何もしない**ため、
  ローカルとの分岐をアプリコードに書かずに済む。**同じ Handler を共有する。**
- **`/tmp` 以外書き込み不可。** `TMPDIR` / `DOTNET_BUNDLE_EXTRACT_BASE_DIR` を `/tmp` にする。
- ビルドは `--platform linux/arm64`（Graviton）。揃えないと `exec format error` で起動しない。

## MySQL プロバイダの選定

**Pomelo ではなく Oracle 公式の `MySql.EntityFrameworkCore` を使う。**
Pomelo は EF Core 9 までしか出ておらず（2026-08 時点）、
EF Core 10 と組み合わせるとバージョン解決で落ちる。
