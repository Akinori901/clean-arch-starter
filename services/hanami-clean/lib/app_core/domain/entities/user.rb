# frozen_string_literal: true

module AppCore
  module Domain
    module Entities
      # User エンティティ。
      #
      # エンティティは「同一性」を持つ。値が変わっても UserId が同じなら同じ User。
      #
      # **ROM の Struct（永続化の型）とは別物であることが重要。**
      # ActiveRecord と違い、Hanami/ROM は「エンティティ＝テーブルの行」ではない。
      # 永続化の都合（created_at の自動採番等）をここに持ち込まないこと。
      class User
        attr_reader :id, :email, :display_name

        def initialize(id:, email:, display_name:, active: true)
          @id = id
          @email = email
          @display_name = display_name
          @active = active
        end

        # 新規ユーザーを組み立てる。表示名はメールアドレスから導出する。
        def self.register(id:, email:)
          new(id: id, email: email, display_name: ValueObjects::DisplayName.from_email(email))
        end

        def active? = @active

        # サインイン可能かを判定する（ビジネスルール）。
        #
        # この判定を Operation や Action の if で書かないこと。
        # ルールをエンティティに置かないと、同じ判定が各所へ散らばる。
        def can_sign_in? = active?

        # 無効化した新しいインスタンスを返す（元は変更しない）。
        def deactivate
          self.class.new(id: id, email: email, display_name: display_name, active: false)
        end

        # 表示名を変えた新しいインスタンスを返す。
        def rename(new_name)
          raise Errors::UserDeactivated, "無効なアカウントは変更できません" unless can_sign_in?

          self.class.new(id: id, email: email, display_name: new_name, active: @active)
        end

        # エンティティの等価性は識別子のみで決まる。
        def ==(other) = other.is_a?(self.class) && other.id == id
        alias eql? ==

        def hash = [self.class, id].hash
      end
    end
  end
end
