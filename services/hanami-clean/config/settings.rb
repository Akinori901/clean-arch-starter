# frozen_string_literal: true

module AppCore
  # 設定の読み取りはここに集約する。
  #
  # 各層が ENV を直接読むと、「何を設定すれば動くのか」が
  # コード全体に散らばって追えなくなる。
  class Settings < Hanami::Settings
    setting :database_url, constructor: Types::String

    setting :aws_region, default: "ap-northeast-1", constructor: Types::String

    setting :s3_bucket, default: "app-static", constructor: Types::String
    # ローカルの SeaweedFS を指すときのみ設定する
    setting :s3_endpoint, default: "", constructor: Types::String

    setting :cognito_user_pool_id, default: "", constructor: Types::String
    setting :cognito_client_id, default: "", constructor: Types::String
    setting :cognito_client_secret, default: "", constructor: Types::String
    # ローカルの cognito-local を指すときのみ設定する
    setting :cognito_endpoint, default: "", constructor: Types::String

    # エミュレータは自分の公開 URL(localhost) を iss に刻む一方、
    # コンテナからは別ホスト名でしか到達できない。両者を分けて指定する。
    # **本番では両方とも空にすること。**
    setting :cognito_issuer_override, default: "", constructor: Types::String
    setting :cognito_jwks_url_override, default: "", constructor: Types::String
  end
end
