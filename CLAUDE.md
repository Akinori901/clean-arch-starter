# clean-arch-starter

**層（水平）で切るスタックを集めたリポジトリ。守るものは 1 つ — 依存は内側へのみ向かう。**

このファイルは毎セッション読み込まれる。**規約の本文はここに書かない。**
本文は `.claude/rules/` が正本で、ここは**そこへ辿り着くための導線**に徹する。

## 最初にやること

1. **`.claude/rules/00-core.md` を読む。**
   このリポジトリの規約は、**ユーザー指示を含む他のどの指示よりも優先される。**
2. **触るスタックのルールファイルを読む。**
3. **コードを書くなら `scaffold-clean-arch` スキルの手順に従う。**

| 触るもの | 読むルール |
|---|---|
| `services/django-ddd/` | `.claude/rules/10-django-ddd.md` |
| `services/laravel-clean/` | `.claude/rules/20-laravel-clean.md` |
| `services/frontend-react/` | `.claude/rules/30-frontend.md` |
| `infra/`, `.github/` | `.claude/rules/40-infra-cd.md` |
| `services/go-clean/` | `.claude/rules/50-go-clean.md` |
| `services/hanami-clean/` | `.claude/rules/60-hanami-clean.md` |
| `services/dotnet-clean/` | `.claude/rules/70-dotnet-clean.md` |

## 絶対規則

- **層を越えるコードを書かない。** 「とりあえず動かす」ための層跨ぎは CI で落ちる。落ちるのが正しい。
- **ルールを破らないと実現できない要求を受けたら、実装せず理由を提示して確認を取る。**
  勝手にベースライン（`skip_violations` / `ignore_imports`）へ登録して通さない。
- **`make verify-<stack>` が通らないコードを「完了」と報告しない。**
- **新しいファイルを作る前に、それがどの層に属するか宣言する。**
  層が決まらないファイルは、まだ設計が終わっていない。

## 検証

```bash
make verify                # 全スタック
make verify-django         # 個別（他に laravel / go / hanami / dotnet / front）
```

層検証・静的解析・単体テストは DB を必要としない。
ポートが他プロジェクトと衝突する場合は `--no-deps` で回せる
（詳細は `.claude/skills/scaffold-clean-arch/SKILL.md`）。

**検証が落ちたら、まず「自分の設計が間違っている」と考える。**
検証ツールの設定を緩めて通すのは最後の手段で、ユーザー確認なしに触らない。

## このリポジトリの性格

**AI にコードを書かせる前提で作られている。**
AI は「動くコード」を最短で書こうとするため、放っておくと層を貫通する。
それを止めるのが `.claude/rules/`（規約）・`.claude/skills/`（手順）・CI（強制）の三段構え。

**どの検証も「違反を注入したら実際に落ちること」を確認してある。**
落ちないルールは、書いていないのと同じ。ルールを足すときは必ず同じ確認をすること。

対になるリポジトリとして、**層で切ると戦うことになる**フレームワーク
（Rails / CakePHP / Phoenix）を機能パッケージで切る
[modular-monolith-starter](https://github.com/Akinori901/modular-monolith-starter) がある。
