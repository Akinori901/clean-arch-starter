# frozen_string_literal: true

require "spec_helper"

RSpec.describe AppCore::Domain::Entities::HealthStatus do
  def component(name, state, detail = "")
    AppCore::Domain::Entities::ComponentHealth.new(name: name, state: state, detail: detail)
  end

  it "全て up なら healthy" do
    status = described_class.new.add(component("database", :up)).add(component("cognito", :up))
    expect(status).to be_healthy
  end

  it "1つでも down なら unhealthy" do
    status = described_class.new
                            .add(component("database", :up))
                            .add(component("cognito", :down, "timeout"))

    expect(status).not_to be_healthy
    expect(status.degraded.map(&:name)).to eq(["cognito"])
  end

  it "確認対象が無い場合は healthy" do
    expect(described_class.new).to be_healthy
  end
end
