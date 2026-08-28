namespace Application.Abstractions;

/// <summary>
/// 外部依存の死活確認の契約。
///
/// 成否は例外で表す。bool を返さないこと（落ちた理由が失われる）。
/// </summary>
public interface IHealthProbe
{
    /// <summary>構成要素名。レスポンスの components[].name になる。</summary>
    string Name { get; }

    /// <summary>疎通確認。失敗したら例外を投げる。</summary>
    Task CheckAsync(CancellationToken cancellationToken);
}
