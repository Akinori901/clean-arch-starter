# .NET (クリーンアーキテクチャ) — 生成手順

対象: `services/dotnet-clean/`
ルール本文: `.claude/rules/70-dotnet-clean.md`（**先に読むこと**）
検証: `make verify-dotnet` / `ProjectReference` + `NetArchTest`

## 雛形にする既存ファイル

| 作るもの | 雛形 |
|---|---|
| 値オブジェクト | `src/Domain/ValueObjects/Email.cs` |
| エンティティ | `src/Domain/Entities/User.cs` |
| ドメイン例外 | `src/Domain/DomainException.cs` |
| 契約(interface) | `src/Application/Abstractions/IUserRepository.cs` |
| ユースケース | `src/Application/UseCases/SignInUseCase.cs` |
| DTO | `src/Application/Dto/SignInResult.cs` |
| Repository 実装 | `src/Infrastructure/Persistence/` 配下 |
| Gateway 実装 | `src/Infrastructure/Cognito/CognitoAuthenticator.cs` |
| DI 結線 | `src/Infrastructure/DependencyInjection.cs` |
| エンドポイント | `src/Web/Endpoints/AuthEndpoints.cs` |
| リクエスト/レスポンス形 | `src/Web/Contracts/Contracts.cs` |
| 例外ハンドラ | `src/Web/DomainExceptionHandler.cs` |
| ドメインのテスト | `tests/Domain.UnitTests/UserTests.cs` |
| ユースケースのテスト | `tests/Application.UnitTests/SignInUseCaseTests.cs`（Fake は `Fakes.cs`） |
| 層検証テスト | `tests/ArchitectureTests/LayerDependencyTests.cs` |

## 生成順序

```
1. src/Domain/ValueObjects/     readonly record struct（不変・値等価性）
2. src/Domain/Entities/         class（**record にしない**）
3. src/Domain/DomainException.cs
4. src/Application/Abstractions/  契約(interface)を **使う側** に置く
5. src/Application/Dto/
6. src/Application/UseCases/    公開は ExecuteAsync のみ
7. src/Infrastructure/          実装クラスは **internal**
8. src/Infrastructure/DependencyInjection.cs   結線口。ここだけ public
9. src/Web/Contracts/ → Endpoints/ → Program.cs
10. tests/
```

## C# 特有の作法

### 1. エンティティは `class`、値オブジェクトは `readonly record struct`

`record` は値等価性を既定にするが、**エンティティの等価性は識別子だけで決まる**。
`record` にすると「表示名を変えたら別人」という誤った等価性になる。

### 2. `Infrastructure` の実装クラスは `internal`

`public` にすると `Web` から直接 `new` できてしまい、
「Web が触れるのは Application の契約だけ」という前提が崩れる。
公開するのは結線口（`DependencyInjection`）と `Options` だけ。
**ArchitectureTests がこれを検証している。**

### 3. 契約は `Application` に置き、`Infrastructure` が実装する

矢印は Infrastructure → Application、つまり外から内を向く（依存性逆転）。

## 検証は 2 段構え（片方では足りない）

| 検証 | 何を守るか | 落ち方 |
|---|---|---|
| ProjectReference | プロジェクト間の依存方向 | `dotnet build` がコンパイルエラー |
| NetArchTest | パッケージ依存・HTTP 語彙の混入・実装クラスの公開 | `dotnet test` が失敗 |

**ProjectReference だけでは NuGet 経由の層破壊が素通りする。**
（`Domain.csproj` に `PackageReference` で EF Core を足す、等）

`.claude/rules/70-dotnet-clean.md` の依存表は **`.csproj` の `ProjectReference` と 1 対 1**。
**表を変えるときは `.csproj` も同時に変える。**

新しい層のルールを足したら `tests/ArchitectureTests/LayerDependencyTests.cs` にも追加し、
**違反を注入して実際に落ちることを確認する**。

## users テーブルは共有物（重要）

`users` は **Django が所有**し、既存スタックと**同じ行を共有する**。

- **EF Core のマイグレーションを作らない。**
  同じテーブルを 2 つのマイグレーション履歴が管理すると必ず壊れる。
  EF Core は「既存テーブルへマップするだけ」に徹する。
- カラム名は `HasColumnName` で **snake_case を明示**（既定は PascalCase で列が見つからない）。
- `created_at` / `updated_at` は DB 側の DEFAULT で入るため `ValueGeneratedOnAdd()` を付ける。
  付けないと `0001-01-01` を送って MySQL が範囲外で拒否する。

## エラーの扱い

- ドメインの例外は `Domain/DomainException.cs`。
- HTTP ステータスへの変換は **`Web` の `DomainExceptionHandler` だけ**。
  各エンドポイントで `try/catch` を書くと変換規則が散らばる。
- **認証失敗時、「ユーザーが存在しない」と「パスワードが違う」を区別しない**
  （アカウント列挙対策。既存全スタックで同一メッセージ）。
- **500 を返すときもログには原因を残す。**

## ハマりどころ（すべて実際に踏んだもの）

- **`ServiceURL` を設定すると `RegionEndpoint` が `null` になる。**
  region は変数に控えてから使う（ヘルスチェックが 500 になった）。
- **cognito-local の `iss` は `localhost:9229` だが、コンテナからは `cognito:9229`。**
  `COGNITO_ISSUER_OVERRIDE` と `COGNITO_JWKS_URL_OVERRIDE` を**別々に**指定する。
- **`ConfigurationManager<OpenIdConnectConfiguration>` は使えない。**
  cognito-local が公開するのは JWKS そのもの。`JsonWebKeySet.Create()` で解釈する。
- **cognito-local は `ExpiresIn` を返さないことがある。** 既定値へフォールバック。
- **Cognito のアクセストークンには `aud` が無い**（代わりに `client_id`）。
  `ValidateAudience = true` のままだと必ず失敗する。
- **AWS SDK の例外は型で判定しない。** `ErrorCode` 文字列で判定する。
- **`FallbackCredentialsFactory` は AWS SDK v4 で廃止。**
  `DefaultAWSCredentialsIdentityResolver` を使う。
- **xunit v3 は `Xunit` 名前空間の暗黙 using を持たない。** `GlobalUsings.cs` で宣言する。
- **.NET 10 は VSTest 経由のテスト実行を廃止。** `global.json` で Microsoft.Testing.Platform に切替。
- **`TestResult` は NetArchTest と Xunit の両方にある。** 明示的に修飾する。
- **MySQL プロバイダは Oracle 公式の `MySql.EntityFrameworkCore`**（Pomelo は EF Core 9 まで）。

## その他

- **`TreatWarningsAsErrors` が有効。** 警告を放置すると「いつもの赤」になって誰も読まなくなる。
- Lambda は `provided.al2023`。`AddAWSLambdaHosting()` は `AWS_LAMBDA_FUNCTION_NAME` が
  無ければ何もしないため、**ローカルとの分岐をアプリコードに書かずに済む**。
- `/tmp` 以外書き込み不可。`TMPDIR` / `DOTNET_BUNDLE_EXTRACT_BASE_DIR` を `/tmp` に。
- ビルドは `--platform linux/arm64`（Graviton）。

## 検証

```bash
make verify-dotnet
# 内訳: dotnet build（ProjectReference が層を強制）→ dotnet test（NetArchTest + 単体）
```
