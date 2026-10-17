<?php

declare(strict_types=1);

namespace Tools\PHPStan\Rules;

use PhpParser\Node;
use PhpParser\Node\Name;
use PHPStan\Analyser\Scope;
use PHPStan\Rules\Rule;
use PHPStan\Rules\RuleErrorBuilder;

/**
 * Service が別の Service を参照していないかを見る。
 *
 * deptrac はレイヤー「間」の依存しか見ず、同じレイヤー内の依存は常に許可する。
 * Service → Service はレイヤーをまたがないので、depfile.yaml に何を書いても落ちない
 * （違反を注入して、deptrac が素通りすることを確認した）。
 *
 * 20-laravel-clean.md は「Service 間の相互呼び出し禁止。複数 Service の調整は
 * UseCase の仕事」と定めているので、それをここで落とす。
 *
 *     public function __construct(private HealthService $health)   ← NG（型宣言）
 *     new HealthService(...)  /  HealthService::class               ← NG
 *
 * PHPStan は名前を完全修飾に解決してから渡すので、use の別名や同じ名前空間からの
 * 参照も、ここでは App\Services\... の形で届く。
 *
 * @implements Rule<Name>
 */
final class NoServiceToServiceRule implements Rule
{
    public function getNodeType(): string
    {
        return Name::class;
    }

    /**
     * @return list<\PHPStan\Rules\IdentifierRuleError>
     */
    public function processNode(Node $node, Scope $scope): array
    {
        $class = $scope->getClassReflection();
        if ($class === null || ! str_starts_with($class->getName(), 'App\\Services\\')) {
            return [];
        }

        $referenced = $node->toString();
        if ($referenced === $class->getName()
            || preg_match('/^App\\\\Services\\\\.+Service$/', $referenced) !== 1) {
            return [];
        }

        return [
            RuleErrorBuilder::message(sprintf(
                '%s が別の Service（%s）を参照しています。'
                .'複数の Service を組み合わせるのは UseCase の仕事です。',
                $class->getName(),
                $referenced,
            ))
                ->identifier('cleanArch.serviceToService')
                ->tip('規約の根拠: .claude/rules/20-laravel-clean.md')
                ->build(),
        ];
    }
}
