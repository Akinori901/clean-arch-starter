namespace Domain.Entities;

/// <summary>構成要素の状態。</summary>
public enum ComponentState
{
    Up,
    Down,
}

/// <summary>個々の依存の状態。</summary>
/// <param name="Name">構成要素名（database / object_storage / cognito）。</param>
/// <param name="State">Up か Down か。</param>
/// <param name="Detail">Down のときの理由。Up のときは空。</param>
public sealed record ComponentHealth(string Name, ComponentState State, string Detail)
{
    // 障害内容が長大なとき、レスポンスとログが膨れるので切り詰める。
    private const int MaxDetailLength = 200;

    public static ComponentHealth Up(string name) => new(name, ComponentState.Up, string.Empty);

    public static ComponentHealth Down(string name, string detail)
    {
        var trimmed = detail.Length > MaxDetailLength ? detail[..MaxDetailLength] : detail;
        return new ComponentHealth(name, ComponentState.Down, trimmed);
    }
}

/// <summary>
/// ヘルスチェック全体の結果。
///
/// 「1 つでも落ちていたら unhealthy」という判定規則はドメインの知識なので、
/// Web 層で if を並べずにここへ置く。
/// </summary>
public sealed class HealthStatus
{
    private readonly List<ComponentHealth> _components = [];

    public IReadOnlyList<ComponentHealth> Components => _components;

    public void Add(ComponentHealth component) => _components.Add(component);

    /// <summary>全構成要素が Up のときのみ true。</summary>
    public bool IsHealthy() => _components.TrueForAll(c => c.State == ComponentState.Up);

    /// <summary>落ちている構成要素だけを返す。</summary>
    public IReadOnlyList<ComponentHealth> Degraded()
        => _components.FindAll(c => c.State != ComponentState.Up);
}
