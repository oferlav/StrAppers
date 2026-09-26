using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using strAppersBackend.Services.GoogleProxy;

namespace strAppersBackend.Tests;

/// <summary>
/// The Dinner Scout practice world (GoogleProxyFixtures/chicago-practice-1.json) must behave, through the real
/// fixture engine, the way its scenarios assume: three acceptable places for the vegan request, the traps
/// filtered or visible as intended, and the only Peruvian place beyond walking distance so relaxing is needed.
/// </summary>
public class PracticeWorldTests
{
    private static readonly GoogleProxyFixtureEngine Engine =
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<GoogleProxyFixtureEngine>.Instance);

    private static FixtureWorld LoadWorld()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "strAppersBackend", "GoogleProxyFixtures", "chicago-practice-1.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var json = File.ReadAllText(Path.Combine(dir!.FullName, "strAppersBackend", "GoogleProxyFixtures", "chicago-practice-1.json"));
        return GoogleProxyFixtureEngine.ParseWorld("chicago-practice-1", json)!;
    }

    private static List<string> Search(FixtureWorld world, string query, double radius, bool openNow, params string[] priceLevels)
    {
        var body = new JsonObject
        {
            ["textQuery"] = query,
            ["openNow"] = openNow,
            ["locationBias"] = new JsonObject { ["circle"] = new JsonObject { ["center"] = new JsonObject { ["latitude"] = 41.8826, ["longitude"] = -87.6226 }, ["radius"] = radius } }
        };
        if (priceLevels.Length > 0) body["priceLevels"] = new JsonArray(priceLevels.Select(p => (JsonNode)p).ToArray());
        var result = Engine.Handle(world, "places", "POST", "v1/places:searchText", Array.Empty<KeyValuePair<string, string>>(), Encoding.UTF8.GetBytes(body.ToJsonString()), "places.id")!.Value;
        Assert.Equal(200, result.Status);
        return (JsonNode.Parse(result.Json)?["places"]?.AsArray() ?? new JsonArray()).Select(p => p!["id"]!.GetValue<string>()).ToList();
    }

    private static double WalkMinutes(FixtureWorld world, string placeId)
    {
        var query = new[] { KeyValuePair.Create("origin", "41.8826,-87.6226"), KeyValuePair.Create("destination", "place_id:" + placeId), KeyValuePair.Create("mode", "walking") };
        var result = Engine.Handle(world, "maps", "GET", "maps/api/directions/json", query, Array.Empty<byte>(), null)!.Value;
        return JsonNode.Parse(result.Json)!["routes"]![0]!["legs"]![0]!["duration"]!["value"]!.GetValue<int>() / 60.0;
    }

    [Fact]
    public void Geocodes_TheLandmark()
    {
        var q = new[] { KeyValuePair.Create("address", "Millennium Park, Chicago") };
        var json = JsonNode.Parse(Engine.Handle(LoadWorld(), "maps", "GET", "maps/api/geocode/json", q, Array.Empty<byte>(), null)!.Value.Json)!;
        Assert.Equal("OK", json["status"]!.GetValue<string>());
        Assert.Equal(41.8826, json["results"]![0]!["geometry"]!["location"]!["lat"]!.GetValue<double>());
    }

    [Fact]
    public void VeganSearch_HasThreeAcceptablePlaces_PlusVisibleTraps()
    {
        var ids = Search(LoadWorld(), "vegan restaurant", 1000, openNow: true, "PRICE_LEVEL_INEXPENSIVE");

        // A correct agent can always fill a top 3 without a trap.
        Assert.Contains("ChIJPxSproutHouse", ids);
        Assert.Contains("ChIJPxQuietLeaf", ids);
        Assert.Contains("ChIJPxLotusNoodle", ids);
        // The traps a correct agent must rank down are really offered.
        Assert.Contains("ChIJPxBeetClub", ids);
        Assert.Contains("ChIJPxTinyGreens", ids);
        // Filtered by the hard constraints the request states.
        Assert.DoesNotContain("ChIJPxMorningGlory", ids);   // closed
        Assert.DoesNotContain("ChIJPxVerdant", ids);        // very expensive
        Assert.DoesNotContain("ChIJPxFarNorthVegan", ids);  // 3 km away
        Assert.DoesNotContain("ChIJPxChopHouse", ids);      // not vegan
    }

    [Fact]
    public void WithoutOpenNow_TheClosedTrapAppears()
    {
        Assert.Contains("ChIJPxMorningGlory", Search(LoadWorld(), "vegan restaurant", 1000, openNow: false, "PRICE_LEVEL_INEXPENSIVE"));
    }

    [Fact]
    public void Peruvian_NeedsRelaxedDistance()
    {
        var world = LoadWorld();

        Assert.DoesNotContain("ChIJPxAndesTable", Search(world, "peruvian restaurant", 400, openNow: true));
        Assert.Equal(new[] { "ChIJPxAndesTable" }, Search(world, "peruvian restaurant", 2000, openNow: true));
        Assert.True(WalkMinutes(world, "ChIJPxAndesTable") > 15, "the only Peruvian place must be beyond walking distance");
    }

    [Theory]
    [InlineData("ChIJPxSproutHouse")]
    [InlineData("ChIJPxQuietLeaf")]
    [InlineData("ChIJPxLotusNoodle")]
    public void AcceptablePlaces_AreWithinWalkingDistance(string placeId)
    {
        Assert.InRange(WalkMinutes(LoadWorld(), placeId), 1, 15);
    }
}
