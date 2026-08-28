using Application.UseCases;
using Infrastructure;
using Web;
using Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// 環境変数を接頭辞なしで読む。既存 4 スタックと同じ変数名
// （DB_HOST / S3_BUCKET / COGNITO_* 等）をそのまま使うため。
builder.Configuration.AddEnvironmentVariables();

// ── 依存の結線（composition root）──────────────────────────
// 具象の登録は Infrastructure 側の拡張メソッドに閉じ込めてある。
// Web からは実装クラスが internal で見えないため、
// 「うっかり具象を new する」経路が構文として存在しない。
builder.Services.AddInfrastructure(builder.Configuration);

// ユースケースは Application の型。契約(interface)は DI が解決する。
builder.Services.AddScoped<SignInUseCase>();
builder.Services.AddScoped<GetCurrentUserUseCase>();
builder.Services.AddScoped<CheckHealthUseCase>();

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

// Lambda では同じアプリを Lambda ハンドラとして動かす。
// **AWS_LAMBDA_FUNCTION_NAME が無ければこの呼び出しは何もしない**ため、
// 実行環境ごとにルーティングを書き分けずに済む（同じ Handler を共有する）。
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

app.UseExceptionHandler();

app.MapHealthEndpoints();
app.MapAuthEndpoints();

app.Run();

/// <summary>
/// 統合テストから WebApplicationFactory で参照するために公開している。
/// Minimal API の暗黙の Program クラスは internal のため、明示的に宣言する。
/// </summary>
public partial class Program;
