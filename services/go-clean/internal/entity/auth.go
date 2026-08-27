package entity

// AuthTokens は認証成功時に発行されるトークン群。
//
// 「認証したらトークンが得られる」はドメインの概念なので entity に置く。
// usecase 側に置くと、実装層(repo)が usecase を import することになり、
// 依存が外から内へ逆流する（go-arch-lint が検知する）。
type AuthTokens struct {
	AccessToken  string
	IDToken      string
	RefreshToken string
	ExpiresIn    int32
}

// VerifiedIdentity は検証済みトークンから取り出した本人情報。
type VerifiedIdentity struct {
	Subject string
	Email   string
}
