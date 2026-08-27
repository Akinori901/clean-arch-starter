# frozen_string_literal: true

module AppCore
  module Domain
    module ValueObjects
      # DisplayName 値オブジェクト。
      class DisplayName
        MAX_LENGTH = 50

        attr_reader :value

        def initialize(value)
          str = value.to_s
          raise Errors::InvalidDisplayName, "表示名が空です" if str.strip.empty?
          raise Errors::InvalidDisplayName, "表示名は#{MAX_LENGTH}文字以内にしてください" if str.length > MAX_LENGTH

          @value = str
          freeze
        end

        # メールアドレスのローカル部を既定の表示名にする。
        def self.from_email(email) = new(email.local_part)

        def to_s = value

        def ==(other) = other.is_a?(self.class) && other.value == value
        alias eql? ==

        def hash = [self.class, value].hash
      end
    end
  end
end
