using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using strAppersBackend.Data;
using strAppersBackend.Models;
using strAppersBackend.Services.GoogleProxy;

namespace strAppersBackend.Tests;

/// <summary>The proxy's fixture mode loads worlds from AgentWorlds by key.</summary>
public class FixtureWorldLoadingTests
{
    private static (GoogleProxyFixtureEngine Engine, ServiceProvider Services) CreateEngine(params AgentWorld[] worlds)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName))
            .BuildServiceProvider();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AgentWorlds.AddRange(worlds);
            db.SaveChanges();
        }
        return (new GoogleProxyFixtureEngine(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<GoogleProxyFixtureEngine>.Instance), services);
    }

    [Fact]
    public async Task LoadsWorldByKey_AndUsesTheKeyAsWorldId()
    {
        var (engine, services) = CreateEngine(new AgentWorld
        {
            Key = "midtown-dinner-1", Name = "Midtown",
            DataJson = """{ "worldId": "something-else", "places": [ { "id": "p1", "displayName": "Green Table", "openNow": true } ] }"""
        });
        using (services)
        {
            var world = await engine.GetWorldAsync("midtown-dinner-1");

            Assert.NotNull(world);
            Assert.Equal("midtown-dinner-1", world!.WorldId);
            Assert.True(Assert.Single(world.Places).OpenNow);
        }
    }

    [Fact]
    public async Task UnknownKey_OrInvalidJson_ReturnsNull()
    {
        var (engine, services) = CreateEngine(new AgentWorld { Key = "broken", Name = "Broken", DataJson = "{ not json" });
        using (services)
        {
            Assert.Null(await engine.GetWorldAsync("nope"));
            Assert.Null(await engine.GetWorldAsync("broken"));
        }
    }
}
