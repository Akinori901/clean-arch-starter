#!/usr/bin/env python
"""Django のコマンドラインユーティリティ。"""
from __future__ import annotations

import os
import sys
from pathlib import Path


def main() -> None:
    # src/ をパスに載せる。層のパッケージ（domain 等）を
    # トップレベルの名前で import できるようにするため。
    sys.path.insert(0, str(Path(__file__).resolve().parent / "src"))
    os.environ.setdefault("DJANGO_SETTINGS_MODULE", "config.settings")

    from django.core.management import execute_from_command_line

    execute_from_command_line(sys.argv)


if __name__ == "__main__":
    main()
