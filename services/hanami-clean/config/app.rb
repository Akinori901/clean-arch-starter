# frozen_string_literal: true

require "hanami"

module AppCore
  class App < Hanami::App
    # JSON ボディを params として受け取れるようにする。
    # これが無いと、application/json の POST で params が空になり
    # バリデーションが必ず失敗する（422 が返り続ける）。
    config.middleware.use :body_parser, :json
  end
end
