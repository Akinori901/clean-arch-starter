# frozen_string_literal: true

require "aws-sdk-cognitoidentityprovider"
require "jwt"
require "net/http"
require "openssl"

module AppCore
  module Gateways
    # 認証基盤（Cognito）のゲートウェイ。
    #
    # **AWS SDK と JWT 検証をここに閉じ込める。**
    # Operation 側は「認証できること」だけを知り、Cognito を知らない。
    #
    # ローカルでは endpoint に cognito-local を指すだけで同じコードが動く。
    # `if local` の分岐をアプリコードに書かないこと。
    class CognitoAuthenticator
      # 「認証情報が正しくない」系のエラーコード。
      # これらを漏らすと 500 になり、認証エラーが障害として扱われてしまう。
      AUTH_FAILURE_CODES = %w[
        NotAuthorizedException
        UserNotFoundException
        InvalidPasswordException
        InvalidParameterException
        UserNotConfirmedException
      ].freeze

      Tokens = Struct.new(:access_token, :id_token, :refresh_token, :expires_in, keyword_init: true)
      Identity = Struct.new(:subject, :email, keyword_init: true)

      def initialize(settings)
        @settings = settings
        @client = Aws::CognitoIdentityProvider::Client.new(
          **{ region: settings.aws_region }.tap do |o|
            o[:endpoint] = settings.cognito_endpoint unless settings.cognito_endpoint.to_s.empty?
          end
        )
      end

      # 認証情報を検証しトークンを発行する。
      def sign_in(email, password)
        params = { "USERNAME" => email.to_s, "PASSWORD" => password }
        params["SECRET_HASH"] = secret_hash(email.to_s) unless @settings.cognito_client_secret.to_s.empty?

        result = @client.initiate_auth(
          client_id: @settings.cognito_client_id,
          auth_flow: "USER_PASSWORD_AUTH",
          auth_parameters: params
        ).authentication_result

        # MFA 等で追加ステップが要求された場合、authentication_result は nil
        raise Domain::Errors::AuthFailed, "追加の認証ステップが必要です" if result.nil?

        Tokens.new(
          access_token: result.access_token,
          id_token: result.id_token,
          refresh_token: result.refresh_token.to_s,
          # 実 Cognito では必ず返るが、エミュレータでは省略されることがある。
          # ここで落とすと本番でだけ動く実装になる。
          expires_in: result.expires_in || 3600
        )
      rescue Aws::CognitoIdentityProvider::Errors::ServiceError => e
        # 「ユーザーが存在しない」と「パスワードが違う」を区別して返さないこと。
        # 区別するとアカウント列挙に使われる。
        raise Domain::Errors::AuthFailed, "メールアドレスまたはパスワードが正しくありません" if auth_failure?(e)

        raise
      end

      # アクセストークンを検証し本人情報を返す。
      def verify_access_token(token)
        claims, = JWT.decode(token, nil, true, algorithms: ["RS256"], jwks: jwks, iss: issuer, verify_iss: true)

        # Cognito のアクセストークンには aud が無く client_id が入るため、明示的に照合する。
        raise Domain::Errors::AuthFailed, "トークンが無効です" unless claims["client_id"] == @settings.cognito_client_id
        raise Domain::Errors::AuthFailed, "アクセストークンではありません" unless claims["token_use"] == "access"

        # アクセストークンに email は含まれないことがある
        Identity.new(subject: claims["sub"], email: claims["email"].to_s)
      rescue JWT::DecodeError
        raise Domain::Errors::AuthFailed, "トークンが無効です"
      end

      # 疎通確認（ヘルスチェック用）
      def ping
        @client.describe_user_pool(user_pool_id: @settings.cognito_user_pool_id)
        nil
      end

      private

      def auth_failure?(error)
        AUTH_FAILURE_CODES.include?(error.class.name.split("::").last)
      end

      def issuer
        override = @settings.cognito_issuer_override.to_s
        return override unless override.empty?

        "https://cognito-idp.#{@settings.aws_region}.amazonaws.com/#{@settings.cognito_user_pool_id}"
      end

      # JWKS の取得先と、トークンに刻まれる issuer は必ずしも一致しない。
      # ローカルのエミュレータは自分の公開 URL(localhost) を iss に刻む一方、
      # コンテナからは別ホスト名でしか到達できないため。
      def jwks_url
        override = @settings.cognito_jwks_url_override.to_s
        override.empty? ? "#{issuer}/.well-known/jwks.json" : override
      end

      # JWKS は都度取りに行くとレート制限に当たり、レイテンシも増える。
      def jwks
        @jwks = nil if @jwks_fetched_at && Time.now - @jwks_fetched_at > 43_200 # 12h
        @jwks ||= begin
          @jwks_fetched_at = Time.now
          JSON.parse(Net::HTTP.get(URI(jwks_url)), symbolize_names: true)
        end
      end

      def secret_hash(username)
        digest = OpenSSL::HMAC.digest(
          "SHA256", @settings.cognito_client_secret, username + @settings.cognito_client_id
        )
        [digest].pack("m0")
      end
    end
  end
end
