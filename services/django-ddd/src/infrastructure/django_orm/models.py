"""Django Model。

**このモジュールを import してよいのは infrastructure 層だけ。**
import-linter の model-containment 契約がこれを強制する。

Model はあくまで「テーブルの写像」であり、ビジネスルールを持たせない。
ルールは domain/entities/ にある。
"""
from __future__ import annotations

from django.db import models
from django.db.models.functions import Now


class UserModel(models.Model):
    # Cognito の sub をそのまま主キーにする（採番を Cognito に委ねる）
    id = models.CharField(primary_key=True, max_length=64)
    email = models.EmailField(unique=True)
    display_name = models.CharField(max_length=100)
    # 集約の内部エンティティ(Profile)も、同じ集約なので同じテーブルに持つ。
    # テーブルを分けるかどうかは永続化の都合であって、集約の境界とは別問題。
    #
    # null=True にしているのは、同じ users テーブルを他サービス(Go 等)も
    # 使うため。Django の default="" は **アプリ側の既定値で DB には出ない**ので、
    # 他サービスが bio を指定せず INSERT すると "no default value" で落ちる。
    bio = models.TextField(blank=True, null=True, default="")
    is_active = models.BooleanField(default=True)

    # auto_now_add / auto_now は **Django が値を入れる**仕組みで、
    # DB 側に DEFAULT は作られない。同じ users テーブルを他サービス(Go 等)も
    # 使うため、DB 側で既定値が入るよう db_default を指定する。
    # これが無いと、他サービスの INSERT が "no default value" で落ちる。
    created_at = models.DateTimeField(db_default=Now())
    updated_at = models.DateTimeField(db_default=Now())

    class Meta:
        db_table = "users"
