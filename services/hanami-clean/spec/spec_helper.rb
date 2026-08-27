# frozen_string_literal: true

# ドメイン層のテストは Hanami を起動せずに動く。
# これが層を分けた実利（DB もフレームワークも要らない）。
require "app_core/domain/errors"
require "app_core/domain/value_objects/email"
require "app_core/domain/value_objects/user_id"
require "app_core/domain/value_objects/display_name"
require "app_core/domain/entities/user"
require "app_core/domain/entities/health_status"

RSpec.configure do |config|
  config.expect_with(:rspec) { |c| c.syntax = :expect }
  config.disable_monkey_patching!
  config.order = :random
end
