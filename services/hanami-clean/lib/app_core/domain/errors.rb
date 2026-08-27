# frozen_string_literal: true

module AppCore
  module Domain
    # ドメインのエラー。
    #
    # **HTTP ステータスコードをここに持ち込まないこと。**
    # 「認証に失敗した」はドメインの語彙だが、「401」は Action の語彙である。
    # 変換は app/actions/ が行う。
    module Errors
      Error = Class.new(StandardError)

      InvalidEmail       = Class.new(Error)
      InvalidUserId      = Class.new(Error)
      InvalidDisplayName = Class.new(Error)
      AuthFailed         = Class.new(Error)
      UserNotFound       = Class.new(Error)
      UserDeactivated    = Class.new(Error)
    end
  end
end
