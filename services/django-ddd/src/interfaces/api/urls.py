from __future__ import annotations

from django.urls import path

from interfaces.api.views import (
    CurrentUserView,
    HealthView,
    LivenessView,
    SignInView,
)

urlpatterns = [
    path("health", HealthView.as_view(), name="health"),
    path("health/live", LivenessView.as_view(), name="liveness"),
    path("auth/sign-in", SignInView.as_view(), name="sign-in"),
    path("auth/me", CurrentUserView.as_view(), name="current-user"),
]
