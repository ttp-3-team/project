using ArchLens.Domain.Compose;
using ArchLens.Domain.Model;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ComposeMappingTests
{
    private static ModelSnapshot Model() => Snapshot(
    [
        Sys("sys"),
        Sys("other"),
        Container("apiGateway", "sys", "API Gateway"),
        Container("legacy", "sys", "Legacy Billing") with { Properties = new Dictionary<string, string> { ["composeService"] = "billing-v2" } },
        Container("worker", "other", "Worker"),
    ]);

    [Theory]
    [InlineData("billing-v2", "legacy")]
    [InlineData("apiGateway", "apiGateway")]
    [InlineData("api-gateway", "apiGateway")]
    [InlineData("legacy_billing", "legacy")]
    public void Service_maps_by_composeService_property_id_or_normalised_name(string service, string container)
    {
        Assert.Equal(container, ServiceMapping.Map(Model(), "sys", [service])[service]);
    }

    [Fact]
    public void Only_containers_of_the_target_system_are_candidates()
    {
        Assert.Empty(ServiceMapping.Map(Model(), "sys", ["worker"]));
    }

    [Fact]
    public void Compose_parses_list_and_map_depends_on()
    {
        var file = ComposeFile.Parse("""
            services:
              b:
                build: .
                depends_on:
                  a:
                    condition: service_healthy
              a:
                image: redis:7
                depends_on: [c]
              c: {}
            """).Value;
        Assert.Equal(["a", "b", "c"], file.Services.Select(s => s.Name));
        Assert.Equal(["c"], file.Services[0].DependsOn);
        Assert.Equal(["a"], file.Services[1].DependsOn);
        Assert.True(file.Services[1].HasBuild);
    }

    [Fact]
    public void Invalid_compose_is_a_value_not_an_exception()
    {
        Assert.True(ComposeFile.Parse("services: [1, 2").IsError);
        Assert.True(ComposeFile.Parse("version: '3'").IsError);
    }

    [Fact]
    public void Non_scalar_service_key_is_a_parse_failure()
    {
        var result = ComposeFile.Parse("services:\n  ? [a, b]\n  : { image: redis }\n");
        Assert.True(result.IsError);
        Assert.Contains("a service name must be a string", result.Descriptions()[0]);
    }
}
