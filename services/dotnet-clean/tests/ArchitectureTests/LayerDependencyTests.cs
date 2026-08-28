using System.Reflection;
using NetArchTest.Rules;

namespace ArchitectureTests;

/// <summary>
/// 層の依存方向を実行時に検証する。
///
/// **ProjectReference（コンパイル時）との 2 段構え。**
/// ProjectReference は「他プロジェクトへの依存」しか縛れないため、
/// 以下はコンパイルを通ってしまう:
///   - Domain が EF Core / AWS SDK の **NuGet パッケージ** を参照する
///   - Domain のエンティティが HTTP の語彙（StatusCode 等）を持つ
/// これらをここで落とす。
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.DomainException).Assembly;
    private static readonly Assembly ApplicationAssembly =
        typeof(Application.Abstractions.IUserRepository).Assembly;
    private static readonly Assembly InfrastructureAssembly =
        typeof(Infrastructure.DependencyInjection).Assembly;

    /// <summary>
    /// Domain は EF Core を知らない。
    ///
    /// ProjectReference では止められない（NuGet パッケージなので）。
    /// Domain.csproj に PackageReference を 1 行足すとここが落ちる。
    /// </summary>
    [Fact]
    public void Domain_はEFCoreに依存しない()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccess(result, "Domain が EF Core に依存しています");
    }

    /// <summary>Domain は AWS SDK を知らない。</summary>
    [Fact]
    public void Domain_はAWSSDKに依存しない()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Amazon")
            .GetResult();

        AssertSuccess(result, "Domain が AWS SDK に依存しています");
    }

    /// <summary>
    /// Domain は HTTP の語彙を知らない。
    ///
    /// 「認証に失敗した」はドメインの語彙だが、「401」は Web の語彙。
    /// エンティティが StatusCode を持ち始めると、ドメインが
    /// 配信手段（HTTP）に縛られる。
    /// </summary>
    [Fact]
    public void Domain_はHTTPの語彙を持たない()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "System.Net.Http",
                "System.Net.HttpStatusCode")
            .GetResult();

        AssertSuccess(result, "Domain が HTTP の語彙に依存しています");
    }

    /// <summary>
    /// Application は Infrastructure を知らない。
    ///
    /// ProjectReference でも循環参照として止まるが、
    /// 「規約として明示する」ために実行時にも確認する。
    /// </summary>
    [Fact]
    public void Application_はInfrastructureに依存しない()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Infrastructure")
            .GetResult();

        AssertSuccess(result, "Application が Infrastructure に依存しています");
    }

    /// <summary>
    /// Application は外部技術を直接知らない。
    ///
    /// ユースケースは「何をするか」であって「どう保存するか」ではない。
    /// EF Core / AWS SDK の型が契約に出てきたら、それは
    /// Infrastructure に置くべきものが漏れている。
    /// </summary>
    [Fact]
    public void Application_は外部技術に依存しない()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Amazon",
                "Microsoft.AspNetCore")
            .GetResult();

        AssertSuccess(result, "Application が外部技術に依存しています");
    }

    /// <summary>Application は Web を知らない（依存は内向きのみ）。</summary>
    [Fact]
    public void Application_はWebに依存しない()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Web")
            .GetResult();

        AssertSuccess(result, "Application が Web に依存しています");
    }

    /// <summary>Infrastructure は Web を知らない（実装層が外部接点を知る理由がない）。</summary>
    [Fact]
    public void Infrastructure_はWebに依存しない()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Web")
            .GetResult();

        AssertSuccess(result, "Infrastructure が Web に依存しています");
    }

    /// <summary>
    /// Infrastructure の実装クラスは外へ公開しない。
    ///
    /// public にすると Web から直接 new できてしまい、
    /// 「Web が触れるのは Application の契約だけ」という前提が崩れる。
    /// 公開してよいのは結線口（DependencyInjection）と設定クラス（Options）だけ。
    /// </summary>
    [Fact]
    public void Infrastructure_の実装クラスは公開しない()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That()
            .AreClasses()
            .And().DoNotHaveName(nameof(Infrastructure.DependencyInjection))
            .And().DoNotHaveNameEndingWith("Options")
            .Should()
            .NotBePublic()
            .GetResult();

        AssertSuccess(result, "Infrastructure の実装クラスが public になっています");
    }

    /// <summary>
    /// 落ちたときに「どの型が違反したか」を必ず出す。
    ///
    /// 「BROKEN」だけ出しても直せない。規約検証は
    /// 落ちた瞬間に原因が分かって初めて機能する。
    /// </summary>
    // TestResult は NetArchTest と Xunit の両方にあるため、明示的に修飾する。
    private static void AssertSuccess(NetArchTest.Rules.TestResult result, string message)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failing = string.Join(
            Environment.NewLine,
            (result.FailingTypeNames ?? []).Select(name => $"  - {name}"));

        Assert.Fail($"{message}{Environment.NewLine}違反している型:{Environment.NewLine}{failing}");
    }
}
