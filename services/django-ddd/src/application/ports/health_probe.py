"""外部依存の死活確認ポート。

DB / ストレージ / 認証基盤それぞれの生存確認を抽象化する。
application は「確認できること」だけを知る。
"""
from __future__ import annotations

from abc import ABC, abstractmethod


class HealthProbePort(ABC):
    @property
    @abstractmethod
    def component_name(self) -> str:
        """ヘルスチェック結果に表示する構成要素名。"""

    @abstractmethod
    def check(self) -> None:
        """疎通確認する。失敗時は例外を送出する（戻り値で成否を返さない）。"""
