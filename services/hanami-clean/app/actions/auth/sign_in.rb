# frozen_string_literal: true

module AppCore
  module Actions
    module Auth
      # Action がやってよいのは 3 つだけ:
      #   1. 入力の検証
      #   2. Operation の呼び出し
      #   3. 応答の組み立て（Failure のタグ → HTTP ステータスへの変換）
      #
      # ビジネスロジックを書かないこと。
      class SignIn < AppCore::Action
        include Deps["operations.auth.sign_in"]

        params do
          required(:email).filled(:string)
          required(:password).filled(:string, min_size?: 8)
        end

        def handle(request, response)
          unless request.params.valid?
            return render_error(response, 422, "メールアドレスとパスワード(8文字以上)は必須です")
          end

          result = sign_in.call(
            email: request.params[:email],
            password: request.params[:password]
          )

          case result
          in Success(tokens:, user:)
            response.status = 200
            response.format = :json
            response.body = {
              access_token: tokens.access_token,
              id_token: tokens.id_token,
              refresh_token: tokens.refresh_token,
              expires_in: tokens.expires_in,
              user: serialize(user)
            }.to_json
          in Failure[tag, message]
            render_error(response, status_for(tag), message)
          end
        end

        private

        def serialize(user)
          {
            user_id: user.id.to_s,
            email: user.email.to_s,
            display_name: user.display_name.to_s,
            is_active: user.can_sign_in?
          }
        end

        # ドメインの語彙（Failure のタグ）を HTTP の語彙へ翻訳する。
        # **この変換を行ってよいのは Action だけ。**
        def status_for(tag)
          {unauthorized: 401, deactivated: 401, not_found: 404, invalid_input: 400}.fetch(tag, 500)
        end

        def render_error(response, status, message)
          response.status = status
          response.format = :json
          response.body = {detail: message}.to_json
        end
      end
    end
  end
end
