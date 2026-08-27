# frozen_string_literal: true

module AppCore
  module Actions
    module Health
      class Show < AppCore::Action
        include Deps["operations.health.check"]

        def handle(_request, response)
          status = check.call.value!

          response.format = :json
          # 依存が落ちていれば 503。ALB はステータスコードで判定するため、
          # 本文が返せていても 200 にしないこと。
          response.status = status.healthy? ? 200 : 503
          response.body = {
            healthy: status.healthy?,
            components: status.components.map do |c|
              { name: c.name, state: c.state.to_s, detail: c.detail }
            end
          }.to_json
        end
      end
    end
  end
end
