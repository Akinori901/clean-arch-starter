# SAM と Terraform — どちらで組むか、併用するならどう分けるか

このリポジトリの**正は SAM**（`infra/sam/template.yaml`）です。
ただし案件の要項で Terraform が指定されることは多いため、
**同じ構成を Terraform で組む場合の対応方針**をここにまとめます。

## まず結論

| 状況 | 選択 |
|---|---|
| Lambda + API Gateway が主役、AWS 内で完結 | **SAM** |
| VPC・RDS・IAM を組織で統一管理している | **Terraform** |
| 案件の要項で Terraform 指定 | **Terraform**（争わない） |
| 既に Terraform 資産がある | **Terraform に寄せる** |
| マルチクラウド | **Terraform** |

判断に迷ったら Terraform を選んで構いません。
**SAM でできることは Terraform でもできます**（逆は成り立ちますが記述量が増えます）。
SAM の利点は記述量とローカル実行（`sam local`）であって、機能的な優位ではありません。

## 記述量の差（実感）

SAM の `AWS::Serverless::Function` は、裏で以下をまとめて作ります。

- Lambda 関数
- 実行ロール（IAM Role）+ 基本の管理ポリシー
- CloudWatch Logs のロググループ
- API Gateway の統合・ルート・権限（`Events` を書いた場合）

Terraform では、これらを**すべて個別のリソースとして書きます**。
本テンプレートの Lambda 2 本ぶんで、おおよそ 40 行 → 200 行前後になります。

```hcl
# SAM の Events: HttpApi 4行に相当するもの
resource "aws_lambda_function" "django" { ... }
resource "aws_iam_role" "django" { ... }
resource "aws_iam_role_policy_attachment" "django_basic" { ... }
resource "aws_iam_role_policy" "django_app" { ... }
resource "aws_cloudwatch_log_group" "django" { ... }
resource "aws_apigatewayv2_integration" "django" { ... }
resource "aws_apigatewayv2_route" "django" { ... }
resource "aws_lambda_permission" "django" { ... }
```

**これは Terraform の欠点ではありません。**
「暗黙に作られるものが無い」ことは、レビューと権限管理の面ではむしろ利点です。

## Terraform へ置き換えるときの対応表

| SAM | Terraform |
|---|---|
| `AWS::Serverless::Function` (Image) | `aws_lambda_function`（`package_type = "Image"`）+ IAM 一式 + ロググループ |
| `Events: HttpApi` | `aws_apigatewayv2_integration` + `_route` + `aws_lambda_permission` |
| `AWS::Serverless::HttpApi` | `aws_apigatewayv2_api` + `_stage` |
| `Policies:` の SAM ポリシーテンプレート | `aws_iam_policy_document` を自分で書く |
| `AWS::Cognito::UserPool` | `aws_cognito_user_pool` |
| `AWS::CloudFront::Distribution` | `aws_cloudfront_distribution` |
| `AWS::CloudFront::OriginAccessControl` | `aws_cloudfront_origin_access_control` |
| `Globals: Function:` | `locals` + 各リソースで展開（Terraform に Globals 相当は無い） |

### 特に注意する点

**1. `Policies:` は自動で展開されない**

SAM の `S3CrudPolicy: {BucketName: x}` は、裏で十数個のアクションを含む
ポリシーへ展開されます。Terraform では自分で書くため、
**移植時に権限が広がりすぎる/狭すぎる事故が起きやすい**箇所です。

移植前に、実際に展開された内容を確認してください。

```bash
sam deploy --no-execute-changeset   # 変更セットだけ作る
# マネジメントコンソール or CLI で展開後の IAM ポリシーを確認して写す
```

**2. ロググループは明示的に作る**

Lambda は初回実行時にロググループを自動作成しますが、
その場合 **保持期間が「無期限」**になり、費用が際限なく増えます。

```hcl
resource "aws_cloudwatch_log_group" "django" {
  name              = "/aws/lambda/${aws_lambda_function.django.function_name}"
  retention_in_days = 30   # 明示しないと無期限＝課金が増え続ける
}
```

**3. `aws_lambda_permission` を忘れない**

SAM の `Events` は API Gateway → Lambda の呼び出し許可も一緒に作ります。
Terraform で書き忘れると、デプロイは成功するのに**実行時だけ 500** になります。

## 併用する場合（現実的にはこれが多い）

**同じリソースを 2 つの IaC で管理しないこと。** 必ず境界を引きます。

| 担当 | 対象 | 理由 |
|---|---|---|
| **Terraform** | VPC / Subnet / RDS / Cognito / S3 / ECR / IAM ロール | 寿命が長く、変更頻度が低い土台 |
| **SAM** | Lambda / API Gateway | デプロイのたびに変わるアプリ層 |

### 受け渡しは SSM Parameter Store 経由で

```hcl
# Terraform 側: 作ったものを SSM へ書き出す
resource "aws_ssm_parameter" "db_host" {
  name  = "/clean-arch-starter/${var.env}/db_host"
  type  = "String"
  value = aws_db_instance.main.address
}
```

```yaml
# SAM 側: SSM から読む（ARN を手で書き写さない）
Parameters:
  DbHost:
    Type: AWS::SSM::Parameter::Value<String>
    Default: /clean-arch-starter/dev/db_host
```

❌ **手でコピペした ARN を SAM テンプレートに直書きしない。**
Terraform 側で作り直したときに、静かに壊れます。

## CI での扱い

`terraform apply` を CI で自動実行しないこと。**必ず `plan` を人が見てから**適用します。

```yaml
# PR では plan だけ流し、結果をコメントする
- run: terraform plan -no-color -out=tfplan
# apply は workflow_dispatch（手動トリガー）に限定する
```

理由は単純で、`plan` を見ずに `apply` すると**意図しない削除**が通ってしまうためです。
特に RDS や Cognito User Pool は、消えると復旧できません
（`prevent_destroy = true` を併用してください）。

```hcl
lifecycle {
  prevent_destroy = true   # RDS / Cognito には必ず付ける
}
```
