# frozen_string_literal: true

module AppCore
  module Operations
    module Health
      # ヘルスチェックのユースケース。
      #
      # 各依存の疎通確認は Gateway / Repo が行う。
      # 「1つでも落ちていたら unhealthy」の判定は HealthStatus エンティティが持つ。
      # ここでは並べて集約するだけ。
      class Check < AppCore::Operation
        include Deps[
          "gateways.cognito_authenticator",
          "gateways.object_storage",
          "relations.users"
        ]

        def call
          # Dry::Operation#call が戻り値を Success で包むため、
          # ここで自分で包まないこと（二重の Success になる）。
          Domain::Entities::HealthStatus.new
                                        .add(probe("database") { users.limit(1).to_a })
                                        .add(probe("object_storage") { object_storage.ping })
                                        .add(probe("cognito") { cognito_authenticator.ping })
        end

        private

        # 1つ落ちても他の確認は続ける。全体像が見えないと切り分けができない。
        def probe(name)
          yield
          Domain::Entities::ComponentHealth.new(name: name, state: :up, detail: "")
        rescue StandardError => e
          Domain::Entities::ComponentHealth.new(name: name, state: :down, detail: e.message[0, 200])
        end
      end
    end
  end
end
