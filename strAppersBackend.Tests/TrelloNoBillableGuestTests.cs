using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Trello bills any guest who is on 2+ boards in a paid workspace. Passing allowBillableGuest=true on
/// board invites told Trello to accept (and bill) those guests instead of refusing them with a 403.
/// These tests keep the flag out of every invite call.
/// </summary>
public class TrelloNoBillableGuestTests
{
    private static TrelloService MakeService(MockHttpMessageHandler handler) =>
        new(new HttpClient(handler),
            Options.Create(new TrelloConfig { ApiKey = "k", ApiToken = "t" }),
            NullLogger<TrelloService>.Instance);

    [Fact]
    public async Task InviteToBoard_DoesNotSendAllowBillableGuest()
    {
        var handler = MockHttpMessageHandler.ReturnOk("{}");

        var (success, _) = await MakeService(handler).InviteMemberToBoardByEmailAsync("board1", "pm@x.com");

        Assert.True(success);
        Assert.DoesNotContain("allowBillableGuest", handler.LastRequest!.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InviteToBoard_MultiBoardGuest403_FailsWithClearMessage()
    {
        var handler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Member not allowed to add a multi-board guest without allowBillableGuest parameter")
        });

        var (success, error) = await MakeService(handler).InviteMemberToBoardByEmailAsync("board1", "pm@x.com");

        Assert.False(success);
        Assert.Contains("billable multi-board guest", error);
    }

    [Fact]
    public void TrelloService_Source_HasNoAllowBillableGuestParameter()
    {
        // The other invite calls sit inside board-creation flows; guard all of them at the source level.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "strAppersBackend", "Services", "TrelloService.cs")))
            dir = dir.Parent;
        Assert.NotNull(dir);

        var source = File.ReadAllText(Path.Combine(dir!.FullName, "strAppersBackend", "Services", "TrelloService.cs"));
        Assert.DoesNotContain("&allowBillableGuest", source, StringComparison.OrdinalIgnoreCase);
    }
}
