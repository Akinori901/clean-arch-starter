# frozen_string_literal: true

module AppCore
  module Operations
    module Auth
      # アクセストークンから現在のユーザーを取得するユースケース。
      class CurrentUser < AppCore::Operation
        include Deps["gateways.cognito_authenticator", "repos.user_repo"]

        def call(access_token:)
          identity = step verify(access_token)

          # Dry::Operation#call が戻り値を Success で包むため、
          # ここで自分で包まないこと（二重の Success になる）。
          step fetch(identity.subject)
        end

        private

        def verify(access_token)
          Success(cognito_authenticator.verify_access_token(access_token))
        rescue Domain::Errors::AuthFailed => e
          Failure[:unauthorized, e.message]
        end

        def fetch(subject)
          user = user_repo.find_by_id(Domain::ValueObjects::UserId.new(subject))
          return Failure[:not_found, "ユーザーが見つかりません"] if user.nil?

          Success(user)
        rescue Domain::Errors::Error => e
          Failure[:invalid_input, e.message]
        end
      end
    end
  end
end
