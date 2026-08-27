"""Django Model。

**このモジュールを import してよいのは infrastructure 層だけ。**
import-linter の model-containment 契約がこれを強制する。

Model はあくまで「テーブルの写像」であり、ビジネスルールを持たせない。
ルールは domain/entities/ にある。
"""
from __future__ import annotations

from django.db import models


class UserModel(models.Model):
    # Cognito の sub をそのまま主キーにする（採番を Cognito に委ねる）
    id = models.CharField(primary_key=True, max_length=64)
    email = models.EmailField(unique=True)
    display_name = models.CharField(max_length=100)
    is_active = models.BooleanField(default=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        db_table = "users"
