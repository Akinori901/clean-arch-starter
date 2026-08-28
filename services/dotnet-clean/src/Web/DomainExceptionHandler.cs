using Domain;
using Microsoft.AspNetCore.Diagnostics;
using Web.Contracts;

namespace Web;

/// <summary>
/// ドメイン例外を HTTP ステータスへ翻訳する。
///
/// **この変換を行ってよいのは Web 層だけ。**
/// Domain や Application が 401 を知る必要はない
/// （ArchitectureTests が Domain への StatusCode 混入を検知する）。
///
/// 各エンドポイントで try/catch を書くと変換規則が散らばるため、
/// IExceptionHandler に集約している。
/// </summary>
internal sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, detail) = exception switch
        {
            AuthenticationFailedException or UserDeactivatedException
                => (StatusCodes.Status401Unauthorized, exception.Message),
            UserNotFoundException
                => (StatusCodes.Status404NotFound, exception.Message),
            InvalidValueException
                => (StatusCodes.Status400BadRequest, exception.Message),
            // 想定外は 500。**レスポンスには詳細を出さないが、ログには必ず残す。**
            // 両方伏せると、本番で原因が追えなくなる。
            _ => (StatusCodes.Status500InternalServerError, "内部エラーが発生しました"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "想定外のエラーが発生しました");
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(detail), cancellationToken);
        return true;
    }
}
