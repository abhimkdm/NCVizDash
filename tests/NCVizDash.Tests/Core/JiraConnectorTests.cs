using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using NCVizDash.Connectors.Jira;
using System.Net;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JiraConnector"/> and <see cref="JiraConnectionProfileStore"/>.
/// Uses a mocked <see cref="HttpMessageHandler"/> so these run with no live network
/// access — appropriate given this sandbox has no route to a real Jira instance.
/// </summary>
public sealed class JiraConnectorTests
{
    private static JiraConnectionProfile MakeProfile() => new()
    {
        ConnectionName = "Test",
        JiraUrl = "https://example.atlassian.net",
        Email = "user@example.com",
        ApiToken = "fake-token"
    };

    private static (Mock<HttpMessageHandler> handler, HttpClient client) MakeHttpMock(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseJson)
            });

        return (handler, new HttpClient(handler.Object));
    }

    private static JiraConnectionProfileStore MakeStoreWithProfile(JiraConnectionProfile profile, out string tempDir)
    {
        // JiraConnectionProfileStore writes to %LOCALAPPDATA%\NCVizDash — redirect
        // via a temp override isn't exposed, so these tests exercise SaveAll/LoadAll
        // round-trip against the real (test-machine) location; acceptable for a
        // lightweight file-based store with no external dependencies to fake.
        var store = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var all = store.LoadAll();
        all.Add(profile);
        store.SaveAll(all);
        tempDir = string.Empty;
        return store;
    }

    [Fact]
    public async Task TestConnectionAsync_SuccessResponse_ReturnsNull()
    {
        var (_, client) = MakeHttpMock("""{"accountId":"123"}""");
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        var error = await sut.TestConnectionAsync(MakeProfile());

        Assert.Null(error);
    }

    [Fact]
    public async Task TestConnectionAsync_Unauthorized_ReturnsErrorMessage()
    {
        var (_, client) = MakeHttpMock("{}", HttpStatusCode.Unauthorized);
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        var error = await sut.TestConnectionAsync(MakeProfile());

        Assert.NotNull(error);
        Assert.Contains("401", error);
    }

    [Fact]
    public async Task ValidateJqlAsync_ValidQuery_ReturnsNull()
    {
        var (_, client) = MakeHttpMock("""{"issues":[]}""");
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        var error = await sut.ValidateJqlAsync(MakeProfile(), "project = NC");

        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateJqlAsync_InvalidQuery_ExtractsErrorMessage()
    {
        var (_, client) = MakeHttpMock(
            """{"errorMessages":["Field 'bogus' does not exist"]}""", HttpStatusCode.BadRequest);
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        var error = await sut.ValidateJqlAsync(MakeProfile(), "bogus = 1");

        Assert.NotNull(error);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public async Task ExecuteJqlAsync_FlattensIssueFields()
    {
        var responseJson = """
        {
          "issues": [
            {
              "key": "NC-1",
              "fields": {
                "summary": "Fix login bug",
                "priority": { "name": "High" },
                "assignee": { "displayName": "Alice" },
                "storyPoints": 5
              }
            }
          ]
        }
        """;

        var (_, client) = MakeHttpMock(responseJson);
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        var rows = await sut.ExecuteJqlAsync(MakeProfile(), "project = NC", maxResults: 10);

        Assert.Single(rows);
        Assert.Equal("NC-1", rows[0]["key"]);
        Assert.Equal("Fix login bug", rows[0]["summary"]);
        Assert.Equal("High", rows[0]["priority"]);         // nested object flattened to display name
        Assert.Equal("Alice", rows[0]["assignee"]);
        Assert.Equal(5L, rows[0]["storyPoints"]);
    }

    [Fact]
    public async Task DiscoverAsync_ParsesConnectionInfo_AndBuildsDescriptor()
    {
        var responseJson = """{"issues":[{"key":"NC-1","fields":{"summary":"Test"}}]}""";
        var (_, client) = MakeHttpMock(responseJson);

        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var profile = MakeProfile();
        var all = profileStore.LoadAll();
        all.Add(profile);
        profileStore.SaveAll(all);

        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);
        var descriptors = await sut.DiscoverAsync($"{profile.Id}||project = NC");

        Assert.Single(descriptors);
        Assert.Equal("Jira", descriptors[0].SourceType);
        Assert.Equal(1, descriptors[0].RowCount);

        // cleanup
        all.RemoveAll(p => p.Id == profile.Id);
        profileStore.SaveAll(all);
    }

    [Fact]
    public async Task DiscoverAsync_MalformedConnectionInfo_ThrowsArgumentException()
    {
        var (_, client) = MakeHttpMock("{}");
        var profileStore = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var sut = new JiraConnector(client, profileStore, NullLogger<JiraConnector>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DiscoverAsync("not-a-valid-format"));
    }

    [Fact]
    public void JiraConnectionProfileStore_SaveThenLoad_RoundTrips()
    {
        var store = new JiraConnectionProfileStore(NullLogger<JiraConnectionProfileStore>.Instance);
        var before = store.LoadAll();

        var profile = MakeProfile();
        profile.FavoriteQueries.Add("project = NC AND sprint in openSprints()");

        var updated = new List<JiraConnectionProfile>(before) { profile };
        store.SaveAll(updated);

        var reloaded = store.LoadAll();
        Assert.Contains(reloaded, p => p.Id == profile.Id && p.FavoriteQueries.Count == 1);

        // cleanup
        store.SaveAll(before);
    }
}
