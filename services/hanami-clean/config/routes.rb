# frozen_string_literal: true

module AppCore
  class Routes < Hanami::Routes
    get "/api/health", to: "health.show"
    get "/api/health/live", to: "health.live"
    post "/api/auth/sign-in", to: "auth.sign_in"
    get "/api/auth/me", to: "auth.me"
  end
end
