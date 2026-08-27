"""Lambda エントリポイント（Mangum で WSGI/ASGI をブリッジする）。

**重い初期化はハンドラの外で済ませる。**
モジュールスコープの処理は実行環境の再利用時にスキップされるため、
コールドスタート時の一度だけで済む。

リクエスト固有の状態をモジュールスコープに置かないこと。
実行環境が再利用されるため、前のリクエストの値が漏れる。
"""
from __future__ import annotations

import os

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")

from django.core.asgi import get_asgi_application  # noqa: E402
from mangum import Mangum  # noqa: E402

application = get_asgi_application()

# API Gateway HTTP API (payload v2) を前提とする。
#
# Mangum の ASGI 型定義は scope を MutableMapping、Django は dict と宣言しており、
# 構造的には互換だが mypy が食い違いとして報告する。実行時は問題なく動く。
handler = Mangum(application, lifespan="off")  # type: ignore[arg-type]
