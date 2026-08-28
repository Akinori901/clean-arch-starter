using Domain.Entities;

namespace Application.Dto;

/// <summary>
/// サインインの結果。
///
/// ユースケースの出力を専用の型に詰め替えることで、
/// Web 層が「トークンとユーザーが返る」という契約だけに依存する。
/// </summary>
public sealed record SignInResult(AuthTokens Tokens, User User);
