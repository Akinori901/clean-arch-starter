# frozen_string_literal: true

module AppCore
  module Actions
    module Auth
      class Me < AppCore::Action
        include Deps["operations.auth.current_user"]

        def handle(request, response)
          token = bearer_token(request)
          return render_error(response, 401, "Authorization ヘッダがありません") if token.nil?

          case current_user.call(access_token: token)
          in Success(user)
            response.status = 200
            response.format = :json
            response.body = {
              user_id: user.id.to_s,
              email: user.email.to_s,
              display_name: user.display_name.to_s,
              is_active: user.can_sign_in?
            }.to_json
          in Failure[tag, message]
            render_error(response, { unauthorized: 401, not_found: 404 }.fetch(tag, 500), message)
          end
        end

        private

        def bearer_token(request)
          header = request.get_header("HTTP_AUTHORIZATION").to_s
          return nil unless header.start_with?("Bearer ")

          token = header.delete_prefix("Bearer ").strip
          token.empty? ? nil : token
        end

        def render_error(response, status, message)
          response.status = status
          response.format = :json
          response.body = { detail: message }.to_json
        end
      end
    end
  end
end
