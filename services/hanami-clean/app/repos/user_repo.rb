# frozen_string_literal: true

module AppCore
  module Repos
    # ユーザーのリポジトリ。
    #
    # **ROM に触れてよい唯一の層。**
    # 返す直前に必ず Struct → ドメインのエンティティへ変換する。
    # Struct をそのまま返すと、永続化の都合が Operation より上へ漏れ出す。
    class UserRepo < AppCore::DB::Repo
      # @return [Domain::Entities::User, nil]
      def find_by_id(user_id)
        to_entity(users.by_pk(user_id.to_s).one)
      end

      # @return [Domain::Entities::User, nil]
      def find_by_email(email)
        to_entity(users.where(email: email.to_s).one)
      end

      # 新規・更新の両方。
      # @return [Domain::Entities::User]
      def save(user)
        attrs = {
          email: user.email.to_s,
          display_name: user.display_name.to_s,
          is_active: user.can_sign_in?
        }

        if users.by_pk(user.id.to_s).exist?
          users.by_pk(user.id.to_s).changeset(:update, attrs).commit
        else
          users.changeset(:create, attrs.merge(id: user.id.to_s)).commit
        end

        find_by_id(user.id)
      end

      private

      # Struct → エンティティ変換。この境界で ROM の都合を断ち切る。
      def to_entity(row)
        return nil if row.nil?

        Domain::Entities::User.new(
          id: Domain::ValueObjects::UserId.new(row[:id]),
          email: Domain::ValueObjects::Email.new(row[:email]),
          display_name: Domain::ValueObjects::DisplayName.new(row[:display_name]),
          active: row[:is_active]
        )
      end
    end
  end
end
