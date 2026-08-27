<?php

declare(strict_types=1);

namespace App\Enums;

/**
 * ヘルスチェック構成要素の状態。
 *
 * Enum は依存グラフの末端。他の層を参照しないこと。
 */
enum ComponentState: string
{
    case Up = 'up';
    case Down = 'down';
}
