using Application.Abstractions;
using Domain.Entities;

namespace Application.UseCases;

/// <summary>各依存の疎通を確認して集約するユースケース。</summary>
public sealed class CheckHealthUseCase(IEnumerable<IHealthProbe> probes)
{
    /// <summary>
    /// 全構成要素を確認する。
    ///
    /// **1 つ落ちても残りの確認は続ける。** 全体像が見えないと切り分けができない。
    /// </summary>
    public async Task<HealthStatus> ExecuteAsync(CancellationToken cancellationToken)
    {
        var status = new HealthStatus();

        foreach (var probe in probes)
        {
            try
            {
                await probe.CheckAsync(cancellationToken);
                status.Add(ComponentHealth.Up(probe.Name));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // ヘルスチェックはどんな例外でも「落ちている」として扱う。
                // ここで握り潰さず、必ず理由を Detail に残す。
                status.Add(ComponentHealth.Down(probe.Name, ex.Message));
            }
        }

        return status;
    }
}
