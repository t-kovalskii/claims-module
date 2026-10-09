using Xunit;

namespace ClaimsModule.Api.IntegrationTests.Fixtures;

/// <summary>
/// spec.md's Testing Decisions: "a real SQL Server instance running in a test container shared
/// across a test collection (not spun up per test, to keep the suite fast)". Every HTTP-seam test
/// class in this assembly declares <c>[Collection(Name)]</c> instead of its own
/// <c>IClassFixture&lt;ApiWebApplicationFactory&gt;</c>, so xUnit boots exactly one container
/// (and runs those classes sequentially against it) for the whole assembly, rather than one
/// container per class racing the others for Docker resources.
/// </summary>
[CollectionDefinition(Name)]
public class ApiTestCollection : ICollectionFixture<ApiWebApplicationFactory>
{
    public const string Name = "Api";
}
