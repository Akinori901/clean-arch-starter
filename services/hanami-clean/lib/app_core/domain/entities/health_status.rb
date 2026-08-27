# frozen_string_literal: true

module AppCore
  module Domain
    module Entities
      # 個々の依存の状態。
      ComponentHealth = Struct.new(:name, :state, :detail, keyword_init: true) do
        def up? = state == :up
      end

      # ヘルスチェック全体の結果。
      #
      # 「1つでも落ちていたら unhealthy」という判定規則はドメインの知識なので、
      # Action 側で if を並べずにここへ置く。
      class HealthStatus
        attr_reader :components

        def initialize(components = [])
          @components = components
        end

        def add(component)
          self.class.new(components + [component])
        end

        def healthy? = components.all?(&:up?)

        def degraded = components.reject(&:up?)
      end
    end
  end
end
