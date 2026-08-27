# frozen_string_literal: true

require "aws-sdk-s3"

module AppCore
  module Gateways
    # オブジェクトストレージ（本番=S3 / ローカル=SeaweedFS）。
    #
    # endpoint を差し替えるだけで両方に対応する。
    # S3 互換 API を使う限り、コードは共通で済む。
    class ObjectStorage
      def initialize(settings)
        @bucket = settings.s3_bucket
        @client = Aws::S3::Client.new(
          **{region: settings.aws_region}.tap { |o|
            unless settings.s3_endpoint.to_s.empty?
              o[:endpoint] = settings.s3_endpoint
              # SeaweedFS 等の S3 互換実装は仮想ホスト形式に対応しないことがある
              o[:force_path_style] = true
            end
          }
        )
      end

      # 疎通確認。オブジェクト一覧ではなく head_bucket を使う。
      # 必要な権限が最小で済み、バケットの中身の量に影響されない。
      def ping
        @client.head_bucket(bucket: @bucket)
        nil
      end
    end
  end
end
