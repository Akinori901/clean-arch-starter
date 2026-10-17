# frozen_string_literal: true

require "hanami"
require "erb"

# Lambda では DB の接続情報を DB_HOST / DB_USER / DB_PASSWORD ... で受け取る
# （SAM テンプレートが Secrets Manager から動的参照で渡す）。他スタックと同じ形に揃えるため。
# Hanami は DATABASE_URL しか読まないので、ここで組み立てる。
# パスワードには @ : / # などが含まれうるので、URL エンコードしてから埋める
# （Sequel が復元することを確認済み）。DATABASE_URL が明示されていればそちらを優先する。
if ENV["DATABASE_URL"].to_s.empty? && !ENV["DB_PASSWORD"].to_s.empty?
  user = ERB::Util.url_encode(ENV.fetch("DB_USER", "app"))
  password = ERB::Util.url_encode(ENV.fetch("DB_PASSWORD"))
  ENV["DATABASE_URL"] = "mysql2://#{user}:#{password}@#{ENV.fetch("DB_HOST", "127.0.0.1")}:" \
                        "#{ENV.fetch("DB_PORT", "3306")}/#{ENV.fetch("DB_NAME", "app")}"
end

module AppCore
  class App < Hanami::App
    # JSON ボディを params として受け取れるようにする。
    # これが無いと、application/json の POST で params が空になり
    # バリデーションが必ず失敗する（422 が返り続ける）。
    config.middleware.use :body_parser, :json
  end
end
