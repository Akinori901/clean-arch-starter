using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Abstractions;
using Infrastructure.Cognito;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Health;

/// <summary>MySQL の疎通確認。</summary>
internal sealed class DatabaseProbe(AppDbContext db) : IHealthProbe
{
    public string Name => "database";

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        // CanConnectAsync は bool を返し、失敗理由を捨ててしまう。
        // 実際にクエリを投げて、例外をそのまま上へ渡す。
        await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
    }
}

/// <summary>
/// S3（本番）/ SeaweedFS（ローカル）の疎通確認。
///
/// endpoint を差し替えるだけで両方に対応する。
/// S3 互換 API を使う限り、コードは共通で済む。
/// </summary>
internal sealed class ObjectStorageProbe(IAmazonS3 s3, IOptions<StorageOptions> options) : IHealthProbe
{
    public string Name => "object_storage";

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        // オブジェクト一覧ではなく HeadBucket 相当を使う。
        // 必要な権限が最小で済み、バケットの中身の量に影響されない。
        await s3.GetBucketLocationAsync(
            new GetBucketLocationRequest { BucketName = options.Value.Bucket },
            cancellationToken);
    }
}

/// <summary>Cognito の疎通確認。</summary>
internal sealed class CognitoProbe(
    IAmazonCognitoIdentityProvider client,
    IOptions<CognitoOptions> options) : IHealthProbe
{
    public string Name => "cognito";

    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        await client.DescribeUserPoolAsync(
            new DescribeUserPoolRequest { UserPoolId = options.Value.UserPoolId },
            cancellationToken);
    }
}

/// <summary>オブジェクトストレージの設定。</summary>
public sealed class StorageOptions
{
    public string Bucket { get; set; } = "app-static";

    /// <summary>ローカルの S3 互換ストレージのエンドポイント。本番では空。</summary>
    public string Endpoint { get; set; } = string.Empty;
}
