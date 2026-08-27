# frozen_string_literal: true

module AppCore
  module Domain
    # ドメインのエラー。
    #
    # **HTTP ステータスコードをここに持ち込まないこと。**
    # 「認証に失敗した」はドメインの語彙だが、「401」は Action の語彙である。
    # 変換は app/actions/ が行う。
    module Errors
      class Error < StandardError
      end

      class InvalidEmail < Error
      end

      class InvalidUserId < Error
      end

      class InvalidDisplayName < Error
      end

      class AuthFailed < Error
      end

      class UserNotFound < Error
      end

      class UserDeactivated < Error
      end
    end
  end
end
