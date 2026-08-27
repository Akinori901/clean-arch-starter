# frozen_string_literal: true

module AppCore
  module Domain
    module ValueObjects
      # UserId 値オブジェクト。Cognito の sub をそのまま識別子として扱う。
      #
      # 素の String を持ち回すと「どの ID なのか」が型から失われるため包む。
      class UserId
        attr_reader :value

        def initialize(value)
          raise Errors::InvalidUserId, "ユーザーIDが空です" if value.to_s.strip.empty?

          @value = value.to_s
          freeze
        end

        def to_s = value

        def ==(other) = other.is_a?(self.class) && other.value == value
        alias eql? ==

        def hash = [self.class, value].hash
      end
    end
  end
end
