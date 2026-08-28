using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Application.Abstractions;
using Infrastructure.Cognito;
using Infrastructure.Health;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Infrastructure の実装を DI コンテナへ登録する。
///
/// 実装クラス（UserRepository / CognitoAuthenticator 等）はすべて internal で、
/// Web からは型として見えない。**Web が触れるのは Application の契約だけ。**
/// 結線をこのメソッドに閉じ込めることで、
/// 「Web が具象を new する」経路が構文として存在しなくなる。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CognitoOptions>(options =>
        {
            options.UserPoolId = configuration["COGNITO_USER_POOL_ID"] ?? string.Empty;
            options.ClientId = configuration["COGNITO_CLIENT_ID"] ?? string.Empty;
            options.ClientSecret = configuration["COGNITO_CLIENT_SECRET"] ?? string.Empty;
            options.Region = configuration["AWS_REGION"] ?? "ap-northeast-1";
            options.Endpoint = configuration["COGNITO_ENDPOINT_URL"] ?? string.Empty;
            options.IssuerOverride = configuration["COGNITO_ISSUER_OVERRIDE"] ?? string.Empty;
            options.JwksUrlOverride = configuration["COGNITO_JWKS_URL_OVERRIDE"] ?? string.Empty;
        });

        services.Configure<StorageOptions>(options =>
        {
            options.Bucket = configuration["S3_BUCKET"] ?? "app-static";
            options.Endpoint = configuration["S3_ENDPOINT_URL"] ?? string.Empty;
        });

        services.AddDbContext<AppDbContext>(options =>
            options.UseMySQL(BuildConnectionString(configuration)));

        services.AddSingleton<IAmazonS3>(_ => CreateS3Client(configuration));
        services.AddSingleton<IAmazonCognitoIdentityProvider>(_ => CreateCognitoClient(configuration));

        services.AddScoped<IUserRepository, UserRepository>();

        // JWKS の取得に HttpClient を使う。IHttpClientFactory 経由にして、
        // ソケットの枯渇と DNS の固着（HttpClient の使い回しで起きる）を避ける。
        services.AddHttpClient<IAuthenticator, CognitoAuthenticator>();

        // ヘルスチェックは複数の IHealthProbe をまとめて注入する。
        // CheckHealthUseCase が IEnumerable<IHealthProbe> で受け取るため、
        // 対象を増やすときはここに 1 行足すだけで済む。
        services.AddScoped<IHealthProbe, DatabaseProbe>();
        services.AddScoped<IHealthProbe, ObjectStorageProbe>();
        services.AddScoped<IHealthProbe, CognitoProbe>();

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var host = configuration["DB_HOST"] ?? "127.0.0.1";
        var port = configuration["DB_PORT"] ?? "3306";
        var name = configuration["DB_NAME"] ?? "app";
        var user = configuration["DB_USER"] ?? "app";
        var password = configuration["DB_PASSWORD"] ?? "app";

        // Lambda では実行環境が再利用されるため、コネクションを絞って使い回す。
        // RDS Proxy を挟む場合はさらに小さくしてよい。
        return $"Server={host};Port={port};Database={name};User Id={user};Password={password};"
               + "CharSet=utf8mb4;Maximum Pool Size=5;Minimum Pool Size=0;";
    }

    private static AmazonS3Client CreateS3Client(IConfiguration configuration)
    {
        var config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                configuration["AWS_REGION"] ?? "ap-northeast-1"),
        };

        // endpoint はローカル（SeaweedFS）のときだけ設定する。
        // 本番では空にして AWS の既定エンドポイントを使う。
        var endpoint = configuration["S3_ENDPOINT_URL"];
        if (!string.IsNullOrEmpty(endpoint))
        {
            config.ServiceURL = endpoint;
            // SeaweedFS 等の S3 互換実装は仮想ホスト形式に対応しないことがある
            config.ForcePathStyle = true;
        }

        return new AmazonS3Client(ResolveCredentials(configuration), config);
    }

    private static AmazonCognitoIdentityProviderClient CreateCognitoClient(IConfiguration configuration)
    {
        var region = configuration["AWS_REGION"] ?? "ap-northeast-1";

        var config = new AmazonCognitoIdentityProviderConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
        };

        var endpoint = configuration["COGNITO_ENDPOINT_URL"];
        if (!string.IsNullOrEmpty(endpoint))
        {
            // **ServiceURL を設定すると RegionEndpoint は null になる。**
            // 代入した直後に config.RegionEndpoint を読むと
            // NullReferenceException で落ちるため、region は変数から取る（実際に踏んだ）。
            config.ServiceURL = endpoint;
            config.AuthenticationRegion = region;
        }

        return new AmazonCognitoIdentityProviderClient(ResolveCredentials(configuration), config);
    }

    /// <summary>
    /// 認証情報を解決する。
    ///
    /// ローカルでは環境変数のダミー値、本番（Lambda）では実行ロールを使う。
    /// **アプリコードに if (local) を書かないため**、環境変数が
    /// 揃っているかどうかだけで判断する。
    /// </summary>
    private static AWSCredentials ResolveCredentials(IConfiguration configuration)
    {
        var accessKey = configuration["AWS_ACCESS_KEY_ID"];
        var secretKey = configuration["AWS_SECRET_ACCESS_KEY"];

        return !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new BasicAWSCredentials(accessKey, secretKey)
            // 本番(Lambda)では実行ロールが解決される。
            // FallbackCredentialsFactory は AWS SDK v4 で廃止された。
            : DefaultAWSCredentialsIdentityResolver.GetCredentials();
    }
}
