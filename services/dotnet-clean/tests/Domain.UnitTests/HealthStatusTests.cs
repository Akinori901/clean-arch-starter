using Domain.Entities;

namespace Domain.UnitTests;

public sealed class HealthStatusTests
{
    [Fact]
    public void 構成要素が空なら健全とみなす()
    {
        Assert.True(new HealthStatus().IsHealthy());
    }

    [Fact]
    public void 全てUpなら健全()
    {
        var status = new HealthStatus();
        status.Add(ComponentHealth.Up("database"));
        status.Add(ComponentHealth.Up("cognito"));

        Assert.True(status.IsHealthy());
        Assert.Empty(status.Degraded());
    }

    [Fact]
    public void 一つでもDownなら不健全()
    {
        // 「1つでも落ちていたら unhealthy」はドメインの判定規則。
        // Web 層の if で書かず、ここに置く。
        var status = new HealthStatus();
        status.Add(ComponentHealth.Up("database"));
        status.Add(ComponentHealth.Down("cognito", "接続できません"));

        Assert.False(status.IsHealthy());
        Assert.Single(status.Degraded());
        Assert.Equal("cognito", status.Degraded()[0].Name);
    }

    [Fact]
    public void Downの理由は二百文字で切り詰める()
    {
        var component = ComponentHealth.Down("database", new string('x', 500));

        Assert.Equal(200, component.Detail.Length);
    }

    [Fact]
    public void Upのときは理由を持たない()
    {
        Assert.Equal(string.Empty, ComponentHealth.Up("database").Detail);
    }
}
