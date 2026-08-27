"""HealthProbePort の各実装。

「疎通できたか」の判定はここ。「全体として健全か」の判定は domain。
責務を混ぜないこと。
"""
from __future__ import annotations

import boto3
from django.db import connection

from application.ports.health_probe import HealthProbePort


class DatabaseProbe(HealthProbePort):
    @property
    def component_name(self) -> str:
        return "database"

    def check(self) -> None:
        with connection.cursor() as cursor:
            cursor.execute("SELECT 1")
            cursor.fetchone()


class ObjectStorageProbe(HealthProbePort):
    """S3（本番）/ SeaweedFS（ローカル）の疎通確認。

    endpoint_url を差し替えるだけで両方に対応する。
    S3 互換 API を使う限り、コードは共通で済む。
    """

    def __init__(
        self, bucket: str, region: str, endpoint_url: str | None = None
    ) -> None:
        self._bucket = bucket
        # region は必ず渡すこと。endpoint_url だけ指定して region を省くと
        # boto3 は NoRegionError で落ちる（ローカルでだけ踏みやすい）。
        self._s3 = boto3.client(
            "s3", region_name=region, endpoint_url=endpoint_url
        )

    @property
    def component_name(self) -> str:
        return "object_storage"

    def check(self) -> None:
        # オブジェクト一覧ではなく HeadBucket を使う。
        # 権限が最小で済み、バケットの中身の量に影響されない。
        self._s3.head_bucket(Bucket=self._bucket)


class CognitoProbe(HealthProbePort):
    def __init__(
        self, user_pool_id: str, region: str, endpoint_url: str | None = None
    ) -> None:
        self._user_pool_id = user_pool_id
        self._client = boto3.client(
            "cognito-idp", region_name=region, endpoint_url=endpoint_url
        )

    @property
    def component_name(self) -> str:
        return "cognito"

    def check(self) -> None:
        self._client.describe_user_pool(UserPoolId=self._user_pool_id)
