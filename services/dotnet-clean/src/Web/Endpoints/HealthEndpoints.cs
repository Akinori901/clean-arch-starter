using Application.UseCases;
using Domain.Entities;
using Web.Contracts;

namespace Web.Endpoints;

/// <summary>ヘルスチェックの HTTP エンドポイント。</summary>
internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", CheckAsync);
        app.MapGet("/api/health/live", () => Results.Ok(new { status = "ok" }));
    }

    private static async Task<IResult> CheckAsync(
        CheckHealthUseCase useCase,
        CancellationToken cancellationToken)
    {
        var status = await useCase.ExecuteAsync(cancellationToken);

        var components = status.Components
            .Select(c => new ComponentResponse(
                c.Name,
                // enum → 文字列は Web 層の関心。Domain 側は "up" という
                // 表現形式を知らない（ComponentState.Up という概念だけ持つ）。
                c.State == ComponentState.Up ? "up" : "down",
                c.Detail))
            .ToList();

        // 依存が落ちていれば 503 を返す。ALB / API Gateway はステータスコードで
        // 判定するため、本文が返せていても 200 にしないこと。
        var statusCode = status.IsHealthy()
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return Results.Json(
            new HealthResponse(status.IsHealthy(), components),
            statusCode: statusCode);
    }
}
