using Application.UseCases;
using Domain.Entities;
using Web.Contracts;

namespace Web.Endpoints;

/// <summary>
/// 認証の HTTP エンドポイント。
///
/// ここでやってよいのは 3 つだけ:
///   1. 入力の検証
///   2. ユースケースの呼び出し
///   3. 応答の組み立て（ドメイン例外 → HTTP ステータスの変換）
///
/// ビジネスロジックを書かないこと。
/// </summary>
internal static class AuthEndpoints
{
    // Cognito の最小パスワード長に合わせた最低限の入力検証。
    // 「正しいか」は Cognito が決めるので、ここでは形だけ見る。
    private const int MinPasswordLength = 8;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/sign-in", SignInAsync);
        app.MapGet("/api/auth/me", GetCurrentUserAsync);
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest? request,
        SignInUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || (request.Password?.Length ?? 0) < MinPasswordLength)
        {
            return Results.BadRequest(
                new ErrorResponse("メールアドレスとパスワード(8文字以上)は必須です"));
        }

        var result = await useCase.ExecuteAsync(request.Email, request.Password!, cancellationToken);

        return Results.Ok(new SignInResponse(
            result.Tokens.AccessToken,
            result.Tokens.IdToken,
            result.Tokens.RefreshToken,
            result.Tokens.ExpiresIn,
            ToResponse(result.User)));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        GetCurrentUserUseCase useCase,
        CancellationToken cancellationToken)
    {
        var token = BearerToken(context);
        if (string.IsNullOrEmpty(token))
        {
            return Results.Json(
                new ErrorResponse("Authorization ヘッダがありません"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await useCase.ExecuteAsync(token, cancellationToken);
        return Results.Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(User user)
        => new(user.Id.Value, user.Email.Value, user.DisplayName.Value, user.IsActive);

    private static string? BearerToken(HttpContext context)
    {
        const string prefix = "Bearer ";
        var header = context.Request.Headers.Authorization.ToString();

        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }
}
