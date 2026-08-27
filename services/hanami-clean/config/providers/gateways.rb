# frozen_string_literal: true

# 外部サービスのゲートウェイを DI コンテナへ登録する。
#
# **具象クラスを結線してよいのはここだけ。**
# Operation / Action が具象を直接 new すると、層の境界が意味を失う。
Hanami.app.register_provider(:gateways) do
  prepare do
    require "app_core/domain/errors"
  end

  start do
    settings = target["settings"]

    register "gateways.cognito_authenticator",
      AppCore::Gateways::CognitoAuthenticator.new(settings)
    register "gateways.object_storage",
      AppCore::Gateways::ObjectStorage.new(settings)
  end
end
