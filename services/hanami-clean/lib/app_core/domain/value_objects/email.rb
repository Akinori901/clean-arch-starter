# frozen_string_literal: true

module AppCore
  module Domain
    module ValueObjects
      # Email 値オブジェクト。
      #
      # 値オブジェクトは不変で、等価性は「値」で決まる。
      # 生成時に検証することで「不正な Email が存在しない」ことを保証する。
      #
      # **この層は Hanami にも ROM にも依存しない。**
      # 素の Ruby だけで書くこと。
      class Email
        PATTERN = /\A[^@\s]+@[^@\s]+\.[^@\s]+\z/

        attr_reader :value

        def initialize(value)
          raise Errors::InvalidEmail, "メールアドレスの形式が不正です: #{value}" unless PATTERN.match?(value.to_s)

          @value = value.to_s
          freeze
        end

        # @ より前を返す。既定の表示名の導出に使う。
        def local_part = value.split("@").first

        def to_s = value

        # 等価性は値で決まる（識別子ではない）
        def ==(other) = other.is_a?(self.class) && other.value == value
        alias eql? ==

        def hash = [self.class, value].hash
      end
    end
  end
end
