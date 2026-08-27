"""DI コンテナ（依存の組立）。

**具象クラスを結線してよいのはここだけ。**
View やユースケースが具象を直接 import すると、層の境界が意味を失う。

外部ライブラリの DI コンテナは使っていない。
この規模なら関数で十分で、依存関係が読んで分かる方が価値が高い。
"""
from __future__ import annotations

from functools import lru_cache

from django.conf import settings

from application.ports.authenticator import AuthenticatorPort
from application.ports.health_probe import HealthProbePort
from application.usecases.check_health import CheckHealthUseCase
from application.usecases.get_current_user import GetCurrentUserUseCase
from application.usecases.sign_in import SignInUseCase
from domain.repositories.user_repository import UserRepository
from infrastructure.cognito.cognito_authenticator import CognitoAuthenticator
from infrastructure.django_orm.repositories.django_user_repository import (
    DjangoUserRepository,
)
from infrastructure.health.probes import CognitoProbe, DatabaseProbe, ObjectStorageProbe


@lru_cache(maxsize=1)
def authenticator() -> AuthenticatorPort:
    # JWKS の取得を伴うため、Lambda の実行環境をまたいで使い回す
    return CognitoAuthenticator(
        user_pool_id=settings.COGNITO["USER_POOL_ID"],
        client_id=settings.COGNITO["CLIENT_ID"],
        client_secret=settings.COGNITO["CLIENT_SECRET"] or None,
        region=settings.COGNITO["REGION"],
        endpoint_url=settings.COGNITO["ENDPOINT_URL"] or None,
        issuer_override=settings.COGNITO["ISSUER_OVERRIDE"] or None,
        jwks_url_override=settings.COGNITO["JWKS_URL_OVERRIDE"] or None,
    )


def user_repository() -> UserRepository:
    return DjangoUserRepository()


def health_probes() -> list[HealthProbePort]:
    return [
        DatabaseProbe(),
        ObjectStorageProbe(
            bucket=settings.OBJECT_STORAGE["BUCKET"],
            region=settings.COGNITO["REGION"],
            endpoint_url=settings.OBJECT_STORAGE["ENDPOINT_URL"] or None,
        ),
        CognitoProbe(
            user_pool_id=settings.COGNITO["USER_POOL_ID"],
            region=settings.COGNITO["REGION"],
            endpoint_url=settings.COGNITO["ENDPOINT_URL"] or None,
        ),
    ]


# --- ユースケースのファクトリ -----------------------------------------
# View はこれらを呼ぶだけでよく、何が注入されるかを知る必要がない。

def sign_in_usecase() -> SignInUseCase:
    return SignInUseCase(authenticator(), user_repository())


def get_current_user_usecase() -> GetCurrentUserUseCase:
    return GetCurrentUserUseCase(authenticator(), user_repository())


def check_health_usecase() -> CheckHealthUseCase:
    return CheckHealthUseCase(health_probes())
