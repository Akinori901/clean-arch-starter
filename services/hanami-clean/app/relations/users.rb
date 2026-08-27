# frozen_string_literal: true

module AppCore
  module Relations
    # ROM のリレーション（テーブルの写像）。
    #
    # **ここに business logic を書かないこと。** スキーマの宣言だけに留める。
    # ActiveRecord と違い、Relation はモデルではなく「クエリの入口」である。
    class Users < AppCore::DB::Relation
      schema :users, infer: true
    end
  end
end
