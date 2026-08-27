package repo

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/base64"
	"errors"
	"fmt"
	"sync"
	"time"

	"github.com/Akinori901/clean-arch-starter/services/go-clean/internal/entity"
	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/service/cognitoidentityprovider"
	"github.com/aws/aws-sdk-go-v2/service/cognitoidentityprovider/types"
	"github.com/aws/smithy-go"
	"github.com/lestrrat-go/jwx/v2/jwk"
	"github.com/lestrrat-go/jwx/v2/jwt"
)

// CognitoConfig は Cognito 接続の設定。
//
// IssuerOverride / JWKSURLOverride はローカルのエミュレータ向け。
// エミュレータは自分の公開 URL(localhost) を iss に刻む一方、
// コンテナからは別ホスト名でしか到達できないため、両者を分けて指定する。
// **本番では両方とも空にすること。**
type CognitoConfig struct {
	UserPoolID      string
	ClientID        string
	ClientSecret    string
	Region          string
	IssuerOverride  string
	JWKSURLOverride string
}

// CognitoAuth は usecase.Authenticator の Cognito 実装。
//
// 契約(interface)は usecase 側で定義され、この型がそれを満たす（依存性逆転）。
// **repo から usecase を import しないこと。** 依存が外から内へ逆流する。
type CognitoAuth struct {
	client  *cognitoidentityprovider.Client
	cfg     CognitoConfig
	issuer  string
	jwksURL string

	// JWKS は都度取りに行くとレート制限に当たり、レイテンシも増える。
	// Lambda では実行環境が再利用されるため、キャッシュが効く。
	mu        sync.RWMutex
	cachedSet jwk.Set
	cachedAt  time.Time
}

const jwksTTL = 12 * time.Hour

// NewCognitoAuth は依存を注入して組み立てる。
func NewCognitoAuth(client *cognitoidentityprovider.Client, cfg CognitoConfig) *CognitoAuth {
	issuer := cfg.IssuerOverride
	if issuer == "" {
		issuer = fmt.Sprintf("https://cognito-idp.%s.amazonaws.com/%s", cfg.Region, cfg.UserPoolID)
	}
	jwksURL := cfg.JWKSURLOverride
	if jwksURL == "" {
		jwksURL = issuer + "/.well-known/jwks.json"
	}
	return &CognitoAuth{client: client, cfg: cfg, issuer: issuer, jwksURL: jwksURL}
}

// SignIn は認証情報を検証しトークンを発行する。
func (a *CognitoAuth) SignIn(ctx context.Context, email entity.Email, password string) (entity.AuthTokens, error) {
	params := map[string]string{"USERNAME": email.String(), "PASSWORD": password}
	if a.cfg.ClientSecret != "" {
		params["SECRET_HASH"] = a.secretHash(email.String())
	}

	out, err := a.client.InitiateAuth(ctx, &cognitoidentityprovider.InitiateAuthInput{
		ClientId:       aws.String(a.cfg.ClientID),
		AuthFlow:       types.AuthFlowTypeUserPasswordAuth,
		AuthParameters: params,
	})
	if err != nil {
		if isAuthFailure(err) {
			// 「ユーザーが存在しない」と「パスワードが違う」を区別して返さないこと。
			// 区別するとアカウント列挙に使われる。
			return entity.AuthTokens{}, entity.ErrAuthFailed
		}
		return entity.AuthTokens{}, fmt.Errorf("認証に失敗: %w", err)
	}

	if out.AuthenticationResult == nil {
		// MFA 等で追加ステップが要求された場合
		return entity.AuthTokens{}, fmt.Errorf("追加の認証ステップが必要です: %s", out.ChallengeName)
	}

	res := out.AuthenticationResult
	expires := res.ExpiresIn
	if expires == 0 {
		// 実 Cognito では必ず返るが、エミュレータでは省略されることがある。
		// ここで落とすと本番でだけ動く実装になる。
		expires = 3600
	}

	return entity.AuthTokens{
		AccessToken:  aws.ToString(res.AccessToken),
		IDToken:      aws.ToString(res.IdToken),
		RefreshToken: aws.ToString(res.RefreshToken),
		ExpiresIn:    expires,
	}, nil
}

// VerifyAccessToken はアクセストークンを検証し本人情報を返す。
func (a *CognitoAuth) VerifyAccessToken(ctx context.Context, raw string) (entity.VerifiedIdentity, error) {
	set, err := a.jwks(ctx)
	if err != nil {
		return entity.VerifiedIdentity{}, fmt.Errorf("JWKS の取得に失敗: %w", err)
	}

	tok, err := jwt.ParseString(raw, jwt.WithKeySet(set), jwt.WithIssuer(a.issuer), jwt.WithValidate(true))
	if err != nil {
		return entity.VerifiedIdentity{}, entity.ErrAuthFailed
	}

	// Cognito のアクセストークンには aud が無く client_id が入るため、明示的に照合する。
	if v, ok := tok.Get("client_id"); !ok || v != a.cfg.ClientID {
		return entity.VerifiedIdentity{}, entity.ErrAuthFailed
	}
	if v, ok := tok.Get("token_use"); !ok || v != "access" {
		return entity.VerifiedIdentity{}, entity.ErrAuthFailed
	}

	identity := entity.VerifiedIdentity{Subject: tok.Subject()}
	// アクセストークンに email は含まれないことがある
	if v, ok := tok.Get("email"); ok {
		if s, ok := v.(string); ok {
			identity.Email = s
		}
	}
	return identity, nil
}

func (a *CognitoAuth) jwks(ctx context.Context) (jwk.Set, error) {
	a.mu.RLock()
	if a.cachedSet != nil && time.Since(a.cachedAt) < jwksTTL {
		defer a.mu.RUnlock()
		return a.cachedSet, nil
	}
	a.mu.RUnlock()

	set, err := jwk.Fetch(ctx, a.jwksURL)
	if err != nil {
		return nil, err
	}

	a.mu.Lock()
	a.cachedSet, a.cachedAt = set, time.Now()
	a.mu.Unlock()
	return set, nil
}

func (a *CognitoAuth) secretHash(username string) string {
	mac := hmac.New(sha256.New, []byte(a.cfg.ClientSecret))
	mac.Write([]byte(username + a.cfg.ClientID))
	return base64.StdEncoding.EncodeToString(mac.Sum(nil))
}

// authFailureCodes は「認証情報が正しくない」系の API エラーコード。
//
// これらを漏らすと 500 になり、認証エラーが障害として扱われてしまう。
var authFailureCodes = map[string]struct{}{
	"NotAuthorizedException":    {},
	"UserNotFoundException":     {},
	"InvalidPasswordException":  {},
	"InvalidParameterException": {},
	"UserNotConfirmedException": {},
}

// isAuthFailure は「認証情報が正しくない」系のエラーかを判定する。
//
// 型アサーション(errors.As)ではなく **エラーコード文字列**で判定している。
// 実 Cognito は型付きの例外を返すが、エミュレータや一部の経路では
// 汎用の APIError として返るため、型で見ると取りこぼす。
// コードは AWS SDK v2 が smithy.APIError として必ず公開する。
func isAuthFailure(err error) bool {
	var apiErr smithy.APIError
	if !errors.As(err, &apiErr) {
		return false
	}
	_, ok := authFailureCodes[apiErr.ErrorCode()]
	return ok
}
