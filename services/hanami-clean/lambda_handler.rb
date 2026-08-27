# frozen_string_literal: true

# Lambda エントリポイント。
#
# **重い初期化はハンドラの外で済ませる。**
# トップレベルの処理は実行環境の再利用時にスキップされるため、
# コールドスタート時の一度だけになる。
#
# リクエスト固有の状態をここに持たせないこと。実行環境が再利用され、
# 前のリクエストの値が漏れる。
require "hanami/boot"
require "rack"
require "json"
require "stringio"

APP = Hanami.app

def handler(event:, context:)
  status, headers, body = APP.call(rack_env(event))

  chunks = []
  body.each { |chunk| chunks << chunk }
  body.close if body.respond_to?(:close)

  {
    statusCode: status,
    headers: headers,
    body: chunks.join
  }
end

# API Gateway HTTP API (payload v2) を Rack env へ変換する。
def rack_env(event)
  http = event.dig("requestContext", "http") || {}
  raw_body = event["body"].to_s
  raw_body = raw_body.unpack1("m") if event["isBase64Encoded"]

  env = {
    "REQUEST_METHOD" => http["method"] || "GET",
    "PATH_INFO" => http["path"] || "/",
    "QUERY_STRING" => event["rawQueryString"].to_s,
    "SERVER_NAME" => "lambda",
    "SERVER_PORT" => "443",
    "rack.input" => StringIO.new(raw_body),
    "rack.url_scheme" => "https"
  }

  (event["headers"] || {}).each do |key, value|
    name = "HTTP_#{key.upcase.tr("-", "_")}"
    env[name] = value
    # Rack は Content-Type / Content-Length を HTTP_ 無しで参照する
    env[key.upcase.tr("-", "_")] = value if %w[content-type content-length].include?(key.downcase)
  end

  env
end
