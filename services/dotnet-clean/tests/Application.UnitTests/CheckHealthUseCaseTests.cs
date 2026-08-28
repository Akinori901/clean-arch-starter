using Application.UseCases;
using Domain.Entities;

namespace Application.UnitTests;

public sealed class CheckHealthUseCaseTests
{
    [Fact]
    public async Task 全て成功なら健全()
    {
        var useCase = new CheckHealthUseCase([
            new FakeHealthProbe("database"),
            new FakeHealthProbe("object_storage"),
        ]);

        var status = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.True(status.IsHealthy());
        Assert.Equal(2, status.Components.Count);
    }

    [Fact]
    public async Task 一つ落ちても残りの確認は続ける()
    {
        // 1 つ目で例外を投げても 3 つ分の結果が返ること。
        // 全体像が見えないと切り分けができない。
        var useCase = new CheckHealthUseCase([
            new FakeHealthProbe("database", new InvalidOperationException("接続できません")),
            new FakeHealthProbe("object_storage"),
            new FakeHealthProbe("cognito"),
        ]);

        var status = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.False(status.IsHealthy());
        Assert.Equal(3, status.Components.Count);
        Assert.Single(status.Degraded());
    }

    [Fact]
    public async Task 落ちた理由が残る()
    {
        var useCase = new CheckHealthUseCase([
            new FakeHealthProbe("database", new InvalidOperationException("接続できません")),
        ]);

        var status = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ComponentState.Down, status.Components[0].State);
        Assert.Contains("接続できません", status.Components[0].Detail, StringComparison.Ordinal);
    }
}
