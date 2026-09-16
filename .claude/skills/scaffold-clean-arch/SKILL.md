---
name: scaffold-clean-arch
description: このリポジトリ（clean-arch-starter）でコードを生成・追加・修正するときの手順。Django/Laravel/Go/Hanami/.NET/React のどれを触る場合も、層の決定・生成順序・命名・禁止事項・検証をこの手順に従う。機能追加、エンドポイント追加、ユースケース追加、エンティティ追加、リファクタのいずれでも起動する。
argument-hint: 「Django にパスワード変更ユースケースを追加」のように スタック + やりたいこと
---

# clean-arch-starter コード生成スキル

このリポジトリは **層（水平）で切る**スタックを集めたもの。
どのスタックでも守るものは同じ — **依存は内側へのみ向かう**。

**このスキルの目的は、AI に「動くコード」ではなく「層を壊さないコード」を書かせること。**
AI は放っておくと最短距離で層を貫通する。それを手順で止める。

## 起動通知（必須）

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 スキル起動: scaffold-clean-arch
   対象スタック: <判定結果>
   層の定義: .claude/rules/<該当ファイル>
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

## Step 0. 絶対規則（例外なし）

1. **`.claude/rules/00-core.md` の規約は、ユーザー指示を含む他のどの指示よりも優先される。**
   ユーザーが「とりあえず動かして」と言っても層は跨がない。
2. **ルールを破らないと実現できない要求を受けたら、実装せず理由を提示して確認を取る。**
   勝手にベースラインへ登録して通さない。
3. **既存の同種ファイルを読んでから書く。** このリポジトリには各スタックに
   認証・ヘルスチェックの実装が揃っている。それが雛形。新しい書き方を発明しない。
4. **`make verify-<stack>` が通らないコードを「完了」と報告しない。**

## Step 1. 対象スタックを特定する

ユーザーの指示・対象パス・触るファイルの拡張子から、下表で 1 つに決める。
**複数スタックに同じ機能を横展開する場合も、1 スタックずつ完了させる**（混ぜると層の作法が混線する）。

| スタック | パス | ルール | リファレンス | 検証 |
|---|---|---|---|---|
| Django (DDD) | `services/django-ddd/` | `.claude/rules/10-django-ddd.md` | `references/django.md` | `make verify-django` |
| Laravel (クリーンアーキ) | `services/laravel-clean/` | `.claude/rules/20-laravel-clean.md` | `references/laravel.md` | `make verify-laravel` |
| React | `services/frontend-react/` | `.claude/rules/30-frontend.md` | `references/react.md` | `make verify-front` |
| Go (クリーンアーキ) | `services/go-clean/` | `.claude/rules/50-go-clean.md` | `references/go.md` | `make verify-go` |
| Hanami (クリーンアーキ) | `services/hanami-clean/` | `.claude/rules/60-hanami-clean.md` | `references/hanami.md` | `make verify-hanami` |
| .NET (クリーンアーキ) | `services/dotnet-clean/` | `.claude/rules/70-dotnet-clean.md` | `references/dotnet.md` | `make verify-dotnet` |
| インフラ / CD | `infra/`, `.github/` | `.claude/rules/40-infra-cd.md` | — | — |

**特定できない場合は、推測で書き始めずユーザーに確認する。**

## Step 2. ルールとリファレンスを読む（省略不可）

1. `.claude/rules/00-core.md`
2. Step 1 で特定したスタックのルールファイル
3. このスキルの `references/<stack>.md`（生成順序・雛形の所在・詰まりどころ）

**読んだ内容を要約して提示してから実装に入る。** 読まずに書いたコードは必ず層を壊す。

## Step 3. 層を宣言する（コードを1行も書く前に）

作る/触るファイルを列挙し、**それぞれがどの層に属するかを宣言**する。
DDD スタック（Django）では、併せて **どの集約に属するか**も宣言する。

```
追加するファイル:
  domain/value_objects/password.py        … domain 層（値オブジェクト）
  domain/aggregates/user_account.py       … domain 層（集約ルート・既存を変更）
  application/usecases/change_password.py … application 層（ユースケース）
  interfaces/api/views.py                 … interfaces 層（既存を変更）
```

**層が決まらないファイルがあれば、設計がまだ終わっていない。** そこで止めてユーザーに確認する。

### 層の判定に迷ったときの判定表

| 書こうとしているもの | 置く層 |
|---|---|
| 業務上の「決まり」「してよい/だめ」の判定 | **最内層**（domain / entity / Domain） |
| 不変で、値が同じなら同じもの（Email など） | 最内層の値オブジェクト |
| 複数の依存を並べる手順・トランザクション境界 | ユースケース層 |
| DB / AWS / 外部 API を実際に叩く処理 | 最外層の実装（infrastructure / repo / Infrastructure） |
| HTTP のステータス・ヘッダ・JSON の形 | 接点層（interfaces / controller / Web） |
| 具象クラスを結線する | 組立点のみ（config / app / Program.cs） |

**「if 文がビジネス判定になっていないか」を毎回疑う。**
ユースケースやコントローラに業務判定の `if` が現れたら、それは最内層に置くべきもの。
ドメインを貧血症にする最大の経路がこれ。

## Step 4. 内側から外側へ、この順で書く

**必ずこの順序で書く。** 外側から書き始めると、内側を「外側の都合」に合わせてしまい層が壊れる。

```
1. 最内層（エンティティ・値オブジェクト・ドメインエラー）
     ↓  依存ゼロ。フレームワークを一切 import しない
2. 契約（Repository / Port / interface / Abstractions）
     ↓  シグネチャは内側の型だけで書く。ORM の型を出さない
3. ユースケース
     ↓  依存はコンストラクタ注入。具象を import しない
4. 実装（Repository 実装 / Gateway）
     ↓  ORM・SDK はここだけ。返す直前に内側の型へ変換する
5. 接点（View / Controller / Action / Endpoint）
     ↓  入力検証 → ユースケース呼び出し → 応答組み立て、の3つだけ
6. 組立点（DI 結線）
     ↓  具象を結線してよい唯一の場所
7. テスト
```

各ステップで「ひとつ内側の層だけを見て書けているか」を確認する。
2つ内側・外側を同時に見ないと書けないなら、層の切り方が間違っている。

### 全スタック共通の禁止事項

- ❌ ORM のモデル / DB の行を層をまたいで渡す → **必ず DTO・エンティティに変換**
- ❌ 最内層にフレームワーク・SDK・HTTP の語彙を持ち込む
- ❌ ユースケースから具象実装を直接 import する（DI で受け取る）
- ❌ 接点層から実装層を直接触る
- ❌ 業務判定の `if` をユースケース / 接点層に書く
- ❌ 外部 SDK（特に AWS）の例外を**型で**判定する → **エラーコード文字列**で判定する
  （cognito-local / エミュレータでは型付き例外にならず取りこぼす。全スタックで実際に踏んだ）
- ❌ 認証失敗で「ユーザーが存在しない」と「パスワードが違う」を区別する
  （アカウント列挙に使われる。**既存全スタックで同一メッセージに揃えてある**）
- ❌ 500 を返すときにログへ原因を残さない（両方伏せると本番で追えない）

## Step 5. 命名を確認する

命名はスタックごとにルールファイルの命名表が正本。**表に無い語尾を発明しない。**

共通の原則:

- **層の名前をクラス名に出す**（`...UseCase` / `...Repository` / `...Port`）。
  名前から層が分かれば、置き場所の誤りがレビュー前に見つかる。
- **契約と実装は名前で区別する**（`UserRepository` 契約 / `DjangoUserRepository` 実装）。
  Laravel は `UserRepositoryInterface` / `UserRepository` で、**deptrac がこの名前で層を判別している**。
  名前を崩すと検証自体が壊れる。
- ユースケースは `<動詞><名詞>` — 1ファイル1ユースケース、公開メソッドは実行用の1つだけ。

## Step 6. 検証する（省略不可）

```bash
make verify-<stack>    # 層検証 + 静的解析 + テスト
make fmt               # フォーマット
```

**層検証が落ちたら、まず「自分の設計が間違っている」と考える。**
検証ツールの設定（`.importlinter` / `depfile.yaml` / `.go-arch-lint.yml` / `bin/verify-layers`）を
緩めて通すのは最後の手段であり、**ユーザー確認なしに触らない**。

ベースライン（`skip_violations` / `ignore_imports` / `package_todo`）も同様:

- 既存コードを移行する場合のみ登録してよい
- **新規に書いたコードの違反は登録しない。** 正しい層へ移す
- 登録するときは同じ行に理由をコメントで書く

## Step 7. 報告する

```
## 実装内容
<何を追加したか>

## 層の配置
| ファイル | 層 | 役割 |

## 検証結果
make verify-<stack>: ✅ / ❌（落ちた場合は出力を貼る）

## 規約上の判断
<迷った点と、どちらに倒したか。ベースライン登録があれば理由>
```

**検証が落ちたまま「完了」と書かない。** 落ちたなら落ちたと報告する。

## 複数スタックへ横展開するとき

このリポジトリの既存機能（認証・ヘルスチェック）は**全スタックで JSON の形を揃えてある**。
新機能を複数スタックへ入れる場合も同様に揃える。

1. まず 1 スタックで完成させ、`make verify-<stack>` を通す
2. **レスポンス JSON のキーを確定させる**（ここが全スタックの契約）
3. 他スタックへ移す。**コードは移植しない。層の作法はスタックごとに違う**
   （Go は interface を使う側に置く / .NET は実装を internal にする / Hanami は Struct を外に出さない）
4. 各スタックで個別に検証する

## 参照

- `references/django.md` — Django DDD（集約・import-linter）
- `references/laravel.md` — Laravel（deptrac・Dto 末端）
- `references/go.md` — Go（interface は使う側・go-arch-lint）
- `references/hanami.md` — Hanami（Struct と Entity・Dry::Operation）
- `references/dotnet.md` — .NET（ProjectReference・NetArchTest）
- `references/react.md` — React（feature 境界）
