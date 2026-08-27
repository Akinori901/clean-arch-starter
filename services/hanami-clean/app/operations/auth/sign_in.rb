# frozen_string_literal: true

module AppCore
  module Operations
    module Auth
      # サインインのユースケース。
      #
      # Hanami では Dry::Operation を使い、**成否を Result（Success/Failure）で返す**。
      # 例外で制御しないことで、呼び出し側（Action）がパターンマッチで分岐できる。
      #
      # 依存は **Deps ミックスイン**で注入される（Hanami の DI コンテナ）。
      # ここで具象クラスを直接 new しないこと。
      class SignIn < AppCore::Operation
        include Deps["gateways.cognito_authenticator", "repos.user_repo"]

        def call(email:, password:)
          email_vo = step build_email(email)

          # 1. 認証基盤（Cognito）で認証する
          tokens = step authenticate(email_vo, password)

          # 2. 検証済みトークンから本人を特定する
          identity = step verify(tokens.access_token)

          # 3. ローカル側のユーザーを解決する（初回サインインなら作る）
          #    Cognito が正で、ローカルはプロフィールの保持のみを担う。
          user = step resolve_user(identity.subject, email_vo)

          # 4. 無効化されたアカウントは、Cognito 側が通しても拒否する。
          #    判定規則はエンティティが持つ。ここでは呼ぶだけ。
          return Failure[:deactivated, "このアカウントは無効化されています"] unless user.can_sign_in?

          # Dry::Operation#call が Success で包むので、素の Hash を返す。
          { tokens: tokens, user: user }
        end

        private

        def build_email(raw)
          Success(Domain::ValueObjects::Email.new(raw))
        rescue Domain::Errors::InvalidEmail => e
          Failure[:invalid_input, e.message]
        end

        def authenticate(email, password)
          Success(cognito_authenticator.sign_in(email, password))
        rescue Domain::Errors::AuthFailed => e
          Failure[:unauthorized, e.message]
        end

        def verify(access_token)
          Success(cognito_authenticator.verify_access_token(access_token))
        rescue Domain::Errors::AuthFailed => e
          Failure[:unauthorized, e.message]
        end

        def resolve_user(subject, email)
          user_id = Domain::ValueObjects::UserId.new(subject)
          existing = user_repo.find_by_id(user_id)
          return Success(existing) if existing

          Success(user_repo.save(Domain::Entities::User.register(id: user_id, email: email)))
        rescue Domain::Errors::Error => e
          Failure[:invalid_input, e.message]
        end
      end
    end
  end
end
