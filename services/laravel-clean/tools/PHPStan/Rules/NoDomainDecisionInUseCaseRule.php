<?php

declare(strict_types=1);

namespace Tools\PHPStan\Rules;

use PhpParser\Node;
use PhpParser\Node\Expr;
use PhpParser\Node\Expr\BinaryOp;
use PhpParser\Node\Expr\BooleanNot;
use PhpParser\Node\Expr\PropertyFetch;
use PhpParser\Node\Stmt;
use PhpParser\Node\Stmt\If_;
use PHPStan\Analyser\Scope;
use PHPStan\Rules\Rule;
use PHPStan\Rules\RuleErrorBuilder;

/**
 * UseCase が「属性を直接読んで業務判定」していないかを見る。
 *
 * deptrac が見るのは「どの層がどの層を参照したか」だけで、
 * 層を越えていない限り通る。だが 20-laravel-clean.md が守りたいのは
 * 「ビジネスロジックは Service が持つ」ことであり、
 * UseCase が Dto の属性を読んで可否を決めた時点でそれは崩れている。
 *
 *     if (! $user->isActive) { ... }                  ← NG。規則が UseCase に漏れている
 *     $this->auth->assertCanSignIn($user);            ← OK。判定は Service が持つ
 *     if ($result === null) { ... }                   ← OK。null チェック
 *
 * **誤検知を出さないことを最優先にする。**
 * 検証はオオカミ少年になった時点で死ぬ（誰も読まなくなり、やがて無効化される）。
 * そのため「業務状態を表す名前」かつ「呼び出しを含まない」条件だけを落とす。
 *
 * @implements Rule<Node>
 */
final class NoDomainDecisionInUseCaseRule implements Rule
{
    /**
     * 業務上の状態を表しやすいプロパティ名の接頭辞。
     */
    private const DOMAIN_STATE_PREFIXES = ['is', 'has', 'can', 'was', 'should'];

    /**
     * 業務上の状態を表しやすいプロパティ名。
     */
    private const DOMAIN_STATE_NAMES = [
        'active', 'enabled', 'disabled', 'deleted', 'expired',
        'approved', 'rejected', 'published', 'locked', 'suspended',
        'role', 'plan', 'tier',
    ];

    // `status` / `state` は**入れない**。
    // PHP・HTTP の文脈では手続きの状態（$resp->status / $cfg->state）が
    // 圧倒的に多く、実測で誤検知した。取りこぼしより誤検知を避ける。

    /**
     * 名前が上の条件に当てはまっても業務判定ではないもの。
     * 手続きの都合・フレームワークの提供物。
     */
    private const NOT_DOMAIN_NAMES = [
        'statusCode', 'isValid', 'isEmpty', 'isFile', 'isDir',
        'isAuthenticated', 'isGuest', 'isDirty', 'isClean',
    ];

    /**
     * レシーバがこれらなら、ドメインの値ではない。
     * フレームワークの提供物・手続きの都合。
     *
     * **`$user` は入れない。** このリポジトリの `UserDto` は `canSignIn()` を
     * 自前で持つドメインの値であり、`$user->isActive` を直接読むのは規約違反。
     * （Django 版は `user` が Django の User モデルなので除外している。
     *   同じ名前でも正体が違うため、判定も異なる）
     */
    private const NOT_DOMAIN_RECEIVERS = [
        'request', 'response', 'resp', 'config', 'settings',
        'options', 'opts', 'client', 'logger',
    ];

    // `result` は**入れない**。
    // `$result = $user; if ($result->isActive)` と書くだけで回避できてしまう。
    // ユースケース内で最も自然に使われる変数名のひとつなので、
    // 除外リストに載せると抜け道そのものになる。
    // 上のリストは「その名前なら中身がフレームワークの物だと断定できる」
    // ものだけに限ること。

    /**
     * 条件分岐は if だけではない。
     * 三項演算子・match・while・elseif も同じ抜け道になるため、
     * Node 全体を受けてから条件式を取り出す。
     * （if だけを見ていたとき、`$x = $user->isActive ? 1 : 0;` が素通りした）
     */
    public function getNodeType(): string
    {
        return Node::class;
    }

    /**
     * @return list<\PHPStan\Rules\IdentifierRuleError>
     */
    public function processNode(Node $node, Scope $scope): array
    {
        $class = $scope->getClassReflection();
        if ($class === null || ! str_starts_with($class->getName(), 'App\\UseCases\\')) {
            return [];
        }

        $cond = $this->conditionOf($node);
        if ($cond === null || ! $this->describesDomainDecision($cond)) {
            return [];
        }

        return [
            RuleErrorBuilder::message(
                'UseCase がプロパティを直接読んで業務判定しています。'
                .'判定規則は Service（またはドメイン）のメソッドへ移し、'
                .'ここではその結果を使ってください。'
            )
                ->identifier('cleanArch.domainDecisionInUseCase')
                ->tip('規約の根拠: .claude/rules/20-laravel-clean.md')
                ->build(),
        ];
    }

    /**
     * 条件分岐のノードから「判定に使われている式」を取り出す。
     * 該当しないノードなら null。
     */
    private function conditionOf(Node $node): ?Expr
    {
        if ($node instanceof If_ || $node instanceof Stmt\ElseIf_) {
            return $node->cond;
        }
        if ($node instanceof Stmt\While_ || $node instanceof Stmt\Do_) {
            return $node->cond;
        }
        if ($node instanceof Expr\Ternary) {
            return $node->cond;
        }
        if ($node instanceof Expr\Match_) {
            return $node->cond;
        }
        if ($node instanceof Stmt\Switch_) {
            return $node->cond;
        }
        // `match (true) { $user->isActive => ... }` の arm 側。
        // match の cond だけを見ていると、この書き方で抜けられる。
        if ($node instanceof Node\MatchArm && $node->conds !== null) {
            foreach ($node->conds as $armCond) {
                if ($this->describesDomainDecision($armCond)) {
                    return $armCond;
                }
            }
        }
        // 変数へ入れてから if する形も拾う（AI が自然に書く抜け道）
        //   $inactive = ! $user->isActive;  if ($inactive) { ... }
        if ($node instanceof Expr\Assign && $node->expr instanceof Expr) {
            return $node->expr;
        }

        return null;
    }

    /**
     * その条件式は「プロパティを直接読んだ業務判定」か。
     *
     * 真を返すのは次をすべて満たす場合だけ。ひとつでも欠ければ通す。
     *   - 条件式に呼び出しが無い（呼んでいるなら判定はその先が持っている）
     *   - 読んでいるプロパティが業務状態を表す名前
     */
    private function describesDomainDecision(Expr $cond): bool
    {
        // not は剥がす
        while ($cond instanceof BooleanNot) {
            $cond = $cond->expr;
        }

        // 論理演算は各項を見る
        if ($cond instanceof BinaryOp\BooleanAnd || $cond instanceof BinaryOp\BooleanOr
            || $cond instanceof BinaryOp\LogicalAnd || $cond instanceof BinaryOp\LogicalOr) {
            return $this->describesDomainDecision($cond->left)
                || $this->describesDomainDecision($cond->right);
        }

        // null 比較は業務判定ではない
        if ($cond instanceof BinaryOp\Identical || $cond instanceof BinaryOp\NotIdentical) {
            if ($this->isNullConst($cond->left) || $this->isNullConst($cond->right)) {
                return false;
            }
        }

        if ($this->containsCall($cond)) {
            // 呼び出しが混ざるなら判定はその先にある。疑わしきは通す。
            return false;
        }

        foreach ($this->collectPropertyFetches($cond) as $fetch) {
            if ($this->isDomainStateProperty($fetch)) {
                return true;
            }
        }

        return false;
    }

    private function isNullConst(Expr $expr): bool
    {
        return $expr instanceof Expr\ConstFetch
            && strtolower($expr->name->toString()) === 'null';
    }

    private function containsCall(Node $node): bool
    {
        if ($node instanceof Expr\MethodCall
            || $node instanceof Expr\StaticCall
            || $node instanceof Expr\FuncCall
            || $node instanceof Expr\NullsafeMethodCall) {
            return true;
        }

        foreach ($node->getSubNodeNames() as $name) {
            $sub = $node->{$name};
            foreach (is_array($sub) ? $sub : [$sub] as $child) {
                if ($child instanceof Node && $this->containsCall($child)) {
                    return true;
                }
            }
        }

        return false;
    }

    /**
     * @return list<PropertyFetch>
     */
    private function collectPropertyFetches(Node $node): array
    {
        $found = [];

        if ($node instanceof PropertyFetch) {
            $found[] = $node;
        }

        foreach ($node->getSubNodeNames() as $name) {
            $sub = $node->{$name};
            foreach (is_array($sub) ? $sub : [$sub] as $child) {
                if ($child instanceof Node) {
                    $found = [...$found, ...$this->collectPropertyFetches($child)];
                }
            }
        }

        return $found;
    }

    private function isDomainStateProperty(PropertyFetch $fetch): bool
    {
        if (! $fetch->name instanceof Node\Identifier) {
            return false;
        }

        $name = $fetch->name->toString();

        if (in_array($name, self::NOT_DOMAIN_NAMES, true)) {
            return false;
        }

        // $this->... は自身の依存であって、ドメインの状態ではない
        if ($fetch->var instanceof Expr\Variable && $fetch->var->name === 'this') {
            return false;
        }

        // フレームワーク・手続きのオブジェクトなら業務判定ではない
        if ($fetch->var instanceof Expr\Variable
            && is_string($fetch->var->name)
            && in_array($fetch->var->name, self::NOT_DOMAIN_RECEIVERS, true)) {
            return false;
        }

        foreach (self::DOMAIN_STATE_PREFIXES as $prefix) {
            // isActive / hasRole のような camelCase を見る。
            // "is" だけの名前は対象外（接頭辞ではなく名前そのもの）。
            if (str_starts_with($name, $prefix)
                && strlen($name) > strlen($prefix)
                && ctype_upper($name[strlen($prefix)])) {
                return true;
            }
        }

        return in_array(strtolower($name), self::DOMAIN_STATE_NAMES, true);
    }
}
