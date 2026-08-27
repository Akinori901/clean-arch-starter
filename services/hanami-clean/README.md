# hanami-clean

Ruby でクリーンアーキテクチャを組む場合のサンプル（Hanami 3）。

規約と検証の詳細は [.claude/rules/60-hanami-clean.md](../../.claude/rules/60-hanami-clean.md) を参照。

```bash
ruby bin/verify-layers                        # 層の依存方向を検証
bundle exec rspec spec/domain -I lib -I spec  # ドメイン層のテスト（Hanami 起動不要）
bundle exec rubocop                           # 静的解析
```
