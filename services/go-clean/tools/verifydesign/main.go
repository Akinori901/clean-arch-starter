// Command verifydesign は、go-arch-lint が見られない設計規約を検証する。
//
// go-arch-lint が見るのは「どの層が何を import したか」だけである。
// だが 50-go-clean.md が禁じていることには、**import では表れないもの**がある。
//
//	if !user.IsActive { ... }     // 層は越えていない。go-arch-lint は通す
//	if !user.CanSignIn() { ... }  // 本来の書き方
//
// 前者は「サインインできるか」という規則を usecase が持ってしまっており、
// entity が貧血症になっている。通し続けると
// **ディレクトリだけクリーンアーキ**のコードが育つ。
//
// 使い方:
//
//	go run ./tools/verifydesign
//
// 設計方針:
//
//	**誤検知を出さないことを最優先にする。**
//	検証はオオカミ少年になった時点で死ぬ。誰も読まなくなり、そのうち無効化される。
//	だから疑わしきは通す側に倒し、確実に言い切れるものだけを落とす。
package main

import (
	"fmt"
	"go/ast"
	"go/parser"
	"go/token"
	"os"
	"path/filepath"
	"strings"
)

type violation struct {
	rule string
	pos  string
	msg  string
	hint string
}

// 業務上の状態を表しやすいフィールド名の接頭辞。
var domainStatePrefixes = []string{"Is", "Has", "Can", "Was", "Should"}

// 業務上の状態を表しやすいフィールド名。
var domainStateNames = map[string]bool{
	"Active": true, "Enabled": true, "Disabled": true, "Deleted": true,
	"Expired": true, "Approved": true, "Rejected": true, "Published": true,
	"Locked": true, "Suspended": true, "Status": true, "State": true,
	"Role": true, "Plan": true, "Tier": true,
}

// 名前が上の条件に当てはまっても業務判定ではないもの。
// 手続きの都合・標準ライブラリの慣習。
var notDomainNames = map[string]bool{
	"StatusCode": true, "IsDir": true, "IsRegular": true, "IsZero": true,
	"IsAbs": true, "IsNil": true,
}

// レシーバがこれらなら、ドメインの値ではない。
//
// **このリストは「その名前なら中身がフレームワーク・手続きの物だと
// 断定できる」ものだけに限ること。**
// 汎用的な名前を載せると、変数名を変えるだけで検証を回避できてしまう。
//
//	u := user
//	if u.IsActive { ... }   // "u" を載せていたら素通りしていた
//
// Laravel 版で "result" を載せていたために
// `$result = $user; if ($result->isActive)` が回避経路になった（実際に踏んだ）。
// 1 文字の変数名（s / c / u 等）も同じ理由で載せない。
var notDomainReceivers = map[string]bool{
	"req": true, "request": true, "resp": true, "response": true,
	"cfg": true, "config": true, "opts": true, "options": true,
	"ctx": true,
}

func main() {
	var violations []violation

	violations = append(violations, checkAnemicDomain("internal/usecase")...)
	violations = append(violations, checkOutwardVocabulary("internal/entity", "entity")...)
	violations = append(violations, checkOutwardVocabulary("internal/usecase", "usecase")...)

	if len(violations) == 0 {
		fmt.Println("設計チェック: OK（違反なし）")
		return
	}

	fmt.Fprintf(os.Stderr, "設計チェック: %d 件の違反\n", len(violations))
	for _, v := range violations {
		fmt.Fprintf(os.Stderr, "  %s: [%s] %s\n", v.pos, v.rule, v.msg)
		fmt.Fprintf(os.Stderr, "      → %s\n", v.hint)
	}
	fmt.Fprintln(os.Stderr)
	fmt.Fprintln(os.Stderr, "規約の根拠: .claude/rules/50-go-clean.md")
	os.Exit(1)
}

// ---------------------------------------------------------------------------
// ルール1: ドメイン判定を usecase に書かない（貧血症の検知）
// ---------------------------------------------------------------------------
//
// 「if の有無」では判定できない。usecase に if があること自体は正しい。
// 見るべきは **何を根拠に分岐しているか**。
//
//	if !user.CanSignIn()   // OK。判定は entity が持つ（メソッド呼び出し）
//	if err != nil          // OK。エラーチェック
//	if !user.IsActive      // NG。フィールドを読んで業務判定している
//
// Go はメソッドとフィールドが構文上見分けられない（どちらも `x.Y`）ため、
// **呼び出し（CallExpr）かどうか**で判別する。

func checkAnemicDomain(dir string) []violation {
	var out []violation

	forEachFile(dir, func(fset *token.FileSet, file *ast.File, path string) {
		ast.Inspect(file, func(n ast.Node) bool {
			for _, cond := range conditionsOf(n) {
				for _, sel := range domainStateSelectors(cond) {
					out = append(out, violation{
						rule: "anemic-domain",
						pos:  fset.Position(sel.Pos()).String(),
						msg: fmt.Sprintf("フィールドを直接読んで業務判定している: %s.%s",
							exprString(sel.X), sel.Sel.Name),
						hint: "判定規則は entity のメソッド（例: CanSignIn()）へ移し、ここでは呼ぶだけにすること",
					})
				}
			}
			return true
		})
	})

	return out
}

// 分岐の条件式を取り出す。該当しなければ空。
//
// **if だけを見ていると抜け道になる。**
// Go では switch が極めて自然な書き方で、悪意なく書いても
// `switch { case !user.IsActive: }` は素通りしていた（実際に踏んだ）。
func conditionsOf(n ast.Node) []ast.Expr {
	switch stmt := n.(type) {
	case *ast.IfStmt:
		return []ast.Expr{stmt.Cond}
	case *ast.ForStmt:
		if stmt.Cond != nil {
			return []ast.Expr{stmt.Cond}
		}
	case *ast.SwitchStmt:
		var out []ast.Expr
		// switch user.IsActive { ... } の Tag
		if stmt.Tag != nil {
			out = append(out, stmt.Tag)
		}
		// switch { case !user.IsActive: } の各 case
		if stmt.Body != nil {
			for _, item := range stmt.Body.List {
				clause, ok := item.(*ast.CaseClause)
				if !ok {
					continue
				}
				out = append(out, clause.List...)
			}
		}
		return out
	}
	return nil
}

// 条件式から「ドメインの状態を読んでいる箇所」だけを取り出す。
//
// **呼び出しの判定は条件式全体ではなく、そのセレクタ自身について行う。**
// 全体で見ると `if !user.IsActive && other.Equals(user)` のように
// 無関係な呼び出しを 1 つ足すだけで検査を丸ごと回避できてしまう（実際に踏んだ）。
func domainStateSelectors(expr ast.Expr) []*ast.SelectorExpr {
	var out []*ast.SelectorExpr

	ast.Inspect(expr, func(n ast.Node) bool {
		// 呼び出しの内側は見ない。`user.CanSignIn()` のように
		// メソッドへ委譲していれば、判定はその先（entity）が持っている。
		if _, ok := n.(*ast.CallExpr); ok {
			return false
		}
		sel, ok := n.(*ast.SelectorExpr)
		if !ok {
			return true
		}
		if !isDomainStateName(sel.Sel.Name) {
			return true
		}
		if ident, ok := sel.X.(*ast.Ident); ok && notDomainReceivers[ident.Name] {
			return true
		}
		out = append(out, sel)
		return true
	})

	return out
}

func isDomainStateName(name string) bool {
	if notDomainNames[name] {
		return false
	}
	for _, p := range domainStatePrefixes {
		if strings.HasPrefix(name, p) && len(name) > len(p) {
			return true
		}
	}
	return domainStateNames[name]
}

// ---------------------------------------------------------------------------
// ルール2: 内側の層に HTTP の語彙を持ち込まない
// ---------------------------------------------------------------------------
//
// HTTP ステータスへの変換は controller だけの仕事で、
// entity / usecase は 401 を知らない（50-go-clean.md）。
//
// 裸の数値（401 等）は見ない。業務上の上限値と区別できず誤検知の温床になる。

var httpVocabulary = map[string]string{
	"StatusCode":     "HTTP ステータス",
	"StatusOK":       "HTTP ステータス",
	"WriteHeader":    "HTTP レスポンス",
	"ResponseWriter": "HTTP レスポンス",
	"http":           "net/http",
}

func checkOutwardVocabulary(dir, layer string) []violation {
	var out []violation

	forEachFile(dir, func(fset *token.FileSet, file *ast.File, path string) {
		ast.Inspect(file, func(n ast.Node) bool {
			ident, ok := n.(*ast.Ident)
			if !ok {
				return true
			}
			label, hit := httpVocabulary[ident.Name]
			if !hit {
				return true
			}
			out = append(out, violation{
				rule: "outward-vocabulary",
				pos:  fset.Position(ident.Pos()).String(),
				msg:  fmt.Sprintf("%s に%sの語彙が現れている: %s", layer, label, ident.Name),
				hint: "HTTP ステータスへの変換は controller の仕事。内側は entity のエラーで表現すること",
			})
			return true
		})
	})

	return out
}

// ---------------------------------------------------------------------------

func forEachFile(dir string, fn func(*token.FileSet, *ast.File, string)) {
	fset := token.NewFileSet()

	_ = filepath.WalkDir(dir, func(path string, d os.DirEntry, err error) error {
		if err != nil || d.IsDir() {
			return nil //nolint:nilerr // 走査できないディレクトリは飛ばす
		}
		if !strings.HasSuffix(path, ".go") || strings.HasSuffix(path, "_test.go") {
			return nil
		}
		file, perr := parser.ParseFile(fset, path, nil, parser.SkipObjectResolution)
		if perr != nil {
			return nil
		}
		fn(fset, file, path)
		return nil
	})
}

func exprString(expr ast.Expr) string {
	if ident, ok := expr.(*ast.Ident); ok {
		return ident.Name
	}
	if sel, ok := expr.(*ast.SelectorExpr); ok {
		return exprString(sel.X) + "." + sel.Sel.Name
	}
	return "?"
}
