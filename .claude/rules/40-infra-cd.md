# 40. インフラ / CD ルール

対象: `infra/`, `.github/workflows/`

## 構成前提

| 対象 | ローカル | AWS |
|---|---|---|
| DB | MySQL 8.4 (Docker) | RDS MySQL |
| オブジェクトストレージ | SeaweedFS (S3 API 互換) | S3 |
| 認証 | Cognito Local (エミュレータ) | Cognito User Pool |
| Django / Laravel | Docker コンテナ | **Lambda**（コンテナイメージ） |
| React | Vite dev server | **S3 + CloudFront** |

**ローカルと本番で同じ SDK・同じコードパスを通すこと。**
`if (local)` による分岐をアプリコードに書かない。差分はすべて環境変数（エンドポイント URL）で吸収する。

## Lambda 配置の制約（アプリコードに効く）

- **書き込み可能なのは `/tmp` のみ**（512MB〜）。ログ・キャッシュ・セッションをローカルFSに置かない。
- **実行ごとにコンテナが再利用される。** グローバル変数に**リクエスト固有の状態を持たせない**。
  DB コネクション等の再利用可能な資源のみモジュールスコープに置く。
- **タイムアウトは API Gateway 側 29 秒が上限。** 長時間処理は SQS へ逃がす。
- **コールドスタート対策**として、ハンドラ外で重い初期化を済ませる。

## IaC の方針

本リポジトリの**正**は `infra/sam/`（AWS SAM）。
ただし案件によって Terraform 指定が多いため、`infra/terraform/` に
**同じ構成を Terraform で組む場合の対応方針**を置いている。
→ 詳細は [docs/iac-sam-vs-terraform.md](../../docs/iac-sam-vs-terraform.md)

### 使い分けの判断

- **SAM**: Lambda + API Gateway が主役で、AWS 内で完結する場合。記述量が圧倒的に少ない。
- **Terraform**: VPC・RDS・IAM を含めて組織で統一管理する場合、
  マルチクラウド、または**案件の要項で指定されている場合**。

### 併用する場合の原則

**同じリソースを 2 つの IaC で管理しない。** 必ず境界を引く。

- Terraform: VPC / Subnet / RDS / Cognito / S3 / IAM ロール（**寿命の長い土台**）
- SAM: Lambda / API Gateway（**デプロイのたびに変わるアプリ層**）
- 受け渡しは Terraform の `output` → SSM Parameter Store → SAM の `Parameter` 参照。
  ❌ 手でコピペした ARN を SAM テンプレートに直書きしない。

## CD の原則

- **AWS 認証は OIDC のみ。** アクセスキーを GitHub Secrets に置かない。
  信頼ポリシーの `sub` は `repo:<owner>/<repo>:*` 形式。
  （個人 AWS アカウントで数値 ID 形式が必要なケースあり → `docs/deploy.md` 参照）
- **`main` push が本番デプロイのトリガー。** それ以外のブランチはデプロイしない。
- **AWS の Secrets が未設定なら、デプロイ系ジョブはスキップする**（`gate` ジョブが判定）。
  テンプレートを clone しただけの状態で CI が赤くなるのを避けるため。
  デプロイするには以下を設定する:
  `AWS_DEPLOY_ROLE_ARN` / `DB_HOST` / `DB_SECRET_ARN` / `VPC_SUBNET_IDS` / `LAMBDA_SG_ID`
  なお `verify`（規約検証）は Secrets の有無にかかわらず常に実行する。
- **デプロイ前に必ず `verify` ジョブを通す。** 層検証が落ちたらデプロイに進まない。
- **フロントは配信後に CloudFront invalidation を打つ。** 打ち忘れると旧 JS が残る。
- `terraform apply` は**必ず `plan` の結果を確認してから**。CI では自動 apply しない。
