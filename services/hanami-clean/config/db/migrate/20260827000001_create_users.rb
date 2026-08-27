# frozen_string_literal: true

# users テーブル。
#
# **他サービス(Django / Go / Laravel)と同じテーブルを共有する。**
# そのため列の既定値は必ず DB 側に持たせること。
# アプリ側だけの既定値にすると、他サービスの INSERT が
# "no default value" で落ちる（実際に踏んだ）。
ROM::SQL.migration do
  change do
    create_table? :users do
      column :id, String, size: 64, primary_key: true
      column :email, String, size: 254, null: false, unique: true
      column :display_name, String, size: 100, null: false
      column :bio, :text, null: true
      column :is_active, TrueClass, null: false, default: true
      column :created_at, DateTime, null: false, default: Sequel::CURRENT_TIMESTAMP
      column :updated_at, DateTime, null: false, default: Sequel::CURRENT_TIMESTAMP
    end
  end
end
