"""入出力の形式変換のみ。ビジネスルールを書かない。"""
from __future__ import annotations

from rest_framework import serializers


class SignInRequestSerializer(serializers.Serializer):
    email = serializers.EmailField()
    password = serializers.CharField(write_only=True, min_length=8)


class SignInResponseSerializer(serializers.Serializer):
    access_token = serializers.CharField()
    id_token = serializers.CharField()
    refresh_token = serializers.CharField()
    expires_in = serializers.IntegerField()
    user_id = serializers.CharField()
    email = serializers.EmailField()
    display_name = serializers.CharField()


class CurrentUserSerializer(serializers.Serializer):
    user_id = serializers.CharField()
    email = serializers.EmailField()
    display_name = serializers.CharField()
    is_active = serializers.BooleanField()


class ComponentSerializer(serializers.Serializer):
    name = serializers.CharField()
    state = serializers.CharField()
    detail = serializers.CharField(allow_blank=True)


class HealthSerializer(serializers.Serializer):
    healthy = serializers.BooleanField()
    components = ComponentSerializer(many=True)
