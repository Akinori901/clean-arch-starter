# Terraform 版（土台のみ）

このディレクトリは **併用パターン**の土台側を示すサンプルです。
アプリ層（Lambda / API Gateway）は `infra/sam/` が担当します。

- 判断基準・移植の対応表 → [docs/iac-sam-vs-terraform.md](../../docs/iac-sam-vs-terraform.md)
- ここで作るもの: VPC / RDS / Cognito / S3 / ECR と、それらを SSM へ書き出す部分

全部を Terraform で組む案件の場合は、この土台に
`aws_lambda_function` 等を足していってください（対応表を参照）。

```bash
terraform init
terraform plan     # 必ず内容を確認する
terraform apply    # plan を見てから
```
