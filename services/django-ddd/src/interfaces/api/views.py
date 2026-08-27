"""DRF View。

View がやってよいのは 3 つだけ:
  1. 入力の検証（Serializer）
  2. UseCase の呼び出し
  3. 応答の組み立て（ドメイン例外 → HTTP ステータスの変換）

ビジネスロジックをここに書かない。
`if` によるドメイン判定が出てきたら、層を間違えている。
"""
from __future__ import annotations

from dataclasses import asdict

from rest_framework import status
from rest_framework.request import Request
from rest_framework.response import Response
from rest_framework.views import APIView

from application.dto.auth_dto import SignInInput
from config import container
from domain.exceptions import (
    AuthenticationFailedError,
    DomainError,
    UserNotFoundError,
)


def _bearer_token(request: Request) -> str | None:
    header = request.headers.get("Authorization", "")
    if not header.startswith("Bearer "):
        return None
    return header.removeprefix("Bearer ").strip() or None


class SignInView(APIView):
    authentication_classes: list = []
    permission_classes: list = []

    def post(self, request: Request) -> Response:
        from interfaces.api.serializers import (
            SignInRequestSerializer,
            SignInResponseSerializer,
        )

        serializer = SignInRequestSerializer(data=request.data)
        serializer.is_valid(raise_exception=True)

        try:
            output = container.sign_in_usecase().execute(
                SignInInput(
                    email=serializer.validated_data["email"],
                    password=serializer.validated_data["password"],
                )
            )
        except AuthenticationFailedError as exc:
            # ドメインの語彙（認証失敗）を HTTP の語彙（401）へ翻訳するのはここ
            return Response(
                {"detail": str(exc)}, status=status.HTTP_401_UNAUTHORIZED
            )
        except DomainError as exc:
            return Response({"detail": str(exc)}, status=status.HTTP_400_BAD_REQUEST)

        return Response(
            SignInResponseSerializer(asdict(output)).data, status=status.HTTP_200_OK
        )


class CurrentUserView(APIView):
    authentication_classes: list = []
    permission_classes: list = []

    def get(self, request: Request) -> Response:
        from interfaces.api.serializers import CurrentUserSerializer

        token = _bearer_token(request)
        if token is None:
            return Response(
                {"detail": "Authorization ヘッダがありません"},
                status=status.HTTP_401_UNAUTHORIZED,
            )

        try:
            output = container.get_current_user_usecase().execute(token)
        except AuthenticationFailedError as exc:
            return Response(
                {"detail": str(exc)}, status=status.HTTP_401_UNAUTHORIZED
            )
        except UserNotFoundError as exc:
            return Response({"detail": str(exc)}, status=status.HTTP_404_NOT_FOUND)

        return Response(
            CurrentUserSerializer(asdict(output)).data, status=status.HTTP_200_OK
        )


class HealthView(APIView):
    authentication_classes: list = []
    permission_classes: list = []

    def get(self, request: Request) -> Response:
        from interfaces.api.serializers import HealthSerializer

        output = container.check_health_usecase().execute()

        # 依存が落ちていれば 503 を返す。ロードバランサ/ALB の判定に使われるため、
        # 本文が返せていても 200 にしないこと。
        code = (
            status.HTTP_200_OK
            if output.healthy
            else status.HTTP_503_SERVICE_UNAVAILABLE
        )
        return Response(HealthSerializer(asdict(output)).data, status=code)


class LivenessView(APIView):
    """プロセスが生きているかだけを見る（依存を確認しない）。

    Lambda では使わないが、ECS/EKS へ載せ替える場合に
    readiness と liveness を分けられるようにしておく。
    """

    authentication_classes: list = []
    permission_classes: list = []

    def get(self, request: Request) -> Response:
        return Response({"status": "ok"}, status=status.HTTP_200_OK)
