# frozen_string_literal: true

require "spec_helper"

RSpec.describe AppCore::Domain::ValueObjects do
  describe AppCore::Domain::ValueObjects::Email do
    it "正しい形式を受け付ける" do
      expect(described_class.new("taro@example.com").to_s).to eq("taro@example.com")
    end

    ["", "no-at-sign", "a@b", "a b@example.com"].each do |bad|
      it "不正な形式を弾く: #{bad.inspect}" do
        expect { described_class.new(bad) }
          .to raise_error(AppCore::Domain::Errors::InvalidEmail)
      end
    end

    it "@ より前を local_part として返す" do
      expect(described_class.new("taro@example.com").local_part).to eq("taro")
    end

    it "等価性は値で決まる（同じ値から作った別インスタンスは等価）" do
      one = described_class.new("a@example.com")
      another = described_class.new("a@example.com")

      expect(one).to eq(another)
    end

    it "不変である" do
      expect(described_class.new("a@example.com")).to be_frozen
    end
  end

  describe AppCore::Domain::ValueObjects::UserId do
    it "空文字を弾く" do
      expect { described_class.new("   ") }
        .to raise_error(AppCore::Domain::Errors::InvalidUserId)
    end
  end

  describe AppCore::Domain::ValueObjects::DisplayName do
    it "上限ちょうど(50文字)は通る" do
      expect { described_class.new("あ" * 50) }.not_to raise_error
    end

    it "上限超過(51文字)は弾く" do
      expect { described_class.new("あ" * 51) }
        .to raise_error(AppCore::Domain::Errors::InvalidDisplayName)
    end

    it "メールアドレスから導出できる" do
      email = AppCore::Domain::ValueObjects::Email.new("taro@example.com")
      expect(described_class.from_email(email).to_s).to eq("taro")
    end
  end
end
