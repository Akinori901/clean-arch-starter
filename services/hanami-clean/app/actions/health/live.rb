# frozen_string_literal: true

module AppCore
  module Actions
    module Health
      # プロセスの生存のみを見る（依存を確認しない）。
      class Live < AppCore::Action
        def handle(_request, response)
          response.format = :json
          response.status = 200
          response.body = {status: "ok"}.to_json
        end
      end
    end
  end
end
