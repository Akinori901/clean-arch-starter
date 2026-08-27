# frozen_string_literal: true

module AppCore
  module Structs
    # ROM の Struct（永続化の型）。
    #
    # **これはドメインのエンティティではない。**
    # Domain::Entities::User と混同しないこと。
    # Struct は「DB から読んだ行」、Entity は「業務上のユーザー」を表す。
    # 変換は Repo が行い、その境界で永続化の都合を断ち切る。
    class User < AppCore::DB::Struct
    end
  end
end
