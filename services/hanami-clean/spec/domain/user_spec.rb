# frozen_string_literal: true

require "spec_helper"

RSpec.describe AppCore::Domain::Entities::User do
  subject(:user) do
    described_class.register(
      id: AppCore::Domain::ValueObjects::UserId.new("sub-1"),
      email: AppCore::Domain::ValueObjects::Email.new("taro@example.com")
    )
  end

  it "表示名をメールアドレスから導出する" do
    expect(user.display_name.to_s).to eq("taro")
  end

  it "新規ユーザーは有効である" do
    expect(user).to be_can_sign_in
  end

  it "無効化するとサインインできなくなる" do
    expect(user.deactivate).not_to be_can_sign_in
  end

  it "無効化は元のインスタンスを変更しない（不変）" do
    user.deactivate
    expect(user).to be_can_sign_in
  end

  it "無効なアカウントは改名できない" do
    expect { user.deactivate.rename(AppCore::Domain::ValueObjects::DisplayName.new("新名")) }
      .to raise_error(AppCore::Domain::Errors::UserDeactivated)
  end

  it "等価性は識別子のみで決まる" do
    other = described_class.register(
      id: AppCore::Domain::ValueObjects::UserId.new("sub-1"),
      email: AppCore::Domain::ValueObjects::Email.new("other@example.com")
    )
    expect(user).to eq(other)
  end
end
