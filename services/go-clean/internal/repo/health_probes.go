package repo

import (
	"context"
	"database/sql"

	"github.com/aws/aws-sdk-go-v2/service/cognitoidentityprovider"
	"github.com/aws/aws-sdk-go-v2/service/s3"
)

// DBProbe は MySQL の疎通確認。
type DBProbe struct{ db *sql.DB }

func NewDBProbe(db *sql.DB) *DBProbe { return &DBProbe{db: db} }

func (p *DBProbe) Name() string { return "database" }

func (p *DBProbe) Check(ctx context.Context) error {
	return p.db.PingContext(ctx)
}

// StorageProbe は S3（本番）/ SeaweedFS（ローカル）の疎通確認。
//
// endpoint を差し替えるだけで両方に対応する。
// S3 互換 API を使う限り、コードは共通で済む。
type StorageProbe struct {
	client *s3.Client
	bucket string
}

func NewStorageProbe(client *s3.Client, bucket string) *StorageProbe {
	return &StorageProbe{client: client, bucket: bucket}
}

func (p *StorageProbe) Name() string { return "object_storage" }

func (p *StorageProbe) Check(ctx context.Context) error {
	// オブジェクト一覧ではなく HeadBucket を使う。
	// 必要な権限が最小で済み、バケットの中身の量に影響されない。
	_, err := p.client.HeadBucket(ctx, &s3.HeadBucketInput{Bucket: &p.bucket})
	return err
}

// CognitoProbe は Cognito の疎通確認。
type CognitoProbe struct {
	client     *cognitoidentityprovider.Client
	userPoolID string
}

func NewCognitoProbe(client *cognitoidentityprovider.Client, userPoolID string) *CognitoProbe {
	return &CognitoProbe{client: client, userPoolID: userPoolID}
}

func (p *CognitoProbe) Name() string { return "cognito" }

func (p *CognitoProbe) Check(ctx context.Context) error {
	_, err := p.client.DescribeUserPool(ctx, &cognitoidentityprovider.DescribeUserPoolInput{
		UserPoolId: &p.userPoolID,
	})
	return err
}
