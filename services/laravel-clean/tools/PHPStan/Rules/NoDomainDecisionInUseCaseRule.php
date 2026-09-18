<?php

declare(strict_types=1);

namespace Tools\PHPStan\Rules;

use PhpParser\Node;
use PhpParser\Node\Expr;
use PhpParser\Node\Expr\BinaryOp;
use PhpParser\Node\Expr\BooleanNot;
use PhpParser\Node\Expr\PropertyFetch;
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
 * @implements Rule<If_>
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
        'status', 'state', 'role', 'plan', 'tier',
    ];

    /**
     * 名前が上の条件に当てはまっても業務判定ではないもの。
     * 手続きの都合・フレームワークの提供物。
     */
    private const NOT_DOMAIN_NAMES = [
        'statusCode', 'isValid', 'isEmpty', 'isFile', 'isDir',
    ];

    public function getNodeType(): string
    {
        return If_::class;
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

        if (! $this->describesDomainDecision($node->cond)) {
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
