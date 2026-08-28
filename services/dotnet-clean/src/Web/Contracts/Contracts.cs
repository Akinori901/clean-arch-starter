using System.Text.Json.Serialization;

namespace Web.Contracts;

/// <summary>
/// HTTP のリクエスト / レスポンス形。
///
/// **JSON のキーは既存 4 スタック（Django / Laravel / Go / Hanami）と揃えてある。**
/// 同じフロントから同じ形で叩けることがこのリポジトリの前提なので、
/// ここを変えるときは 5 スタックすべてを同時に変えること。
/// </summary>
public sealed record SignInRequest(
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("password")] string? Password);

public sealed record UserResponse(
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("is_active")] bool IsActive);

public sealed record SignInResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("user")] UserResponse User);

public sealed record ComponentResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("detail")] string Detail);

public sealed record HealthResponse(
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("components")] IReadOnlyList<ComponentResponse> Components);

public sealed record ErrorResponse(
    [property: JsonPropertyName("detail")] string Detail);
