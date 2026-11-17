using System.Net;
using System.Text;
using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class FirebaseRestTests
{
    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Body, string? IfMatch)> Seen { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
            Seen.Add((request.Method.Method, request.RequestUri!.ToString(), body,
                request.Headers.TryGetValues("if-match", out var m) ? m.Single() : null));
            return answer(request);
        }
    }

    private static HttpResponseMessage Json(string text, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(text, Encoding.UTF8, "application/json") };

    private static (FirebaseRest Db, Fake Fake) Make(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var fake = new Fake(answer);
        return (new FirebaseRest(new HttpClient(fake), new FirebaseOptions { DatabaseUrl = "https://db.example/" }), fake);
    }

    [Fact]
    public async Task Builds_urls_from_paths_and_queries()
    {
        var (db, fake) = Make(_ => Json("{\"a\":true}"));
        Assert.Equal("https://db.example/users/a%20b.json", db.Url($"users/{FirebaseRest.Key("a b")}"));
        Assert.Equal("https://db.example/.json", db.Url(""));
        await db.GetAsync<Dictionary<string, bool>>("users/x", "shallow=true");
        Assert.Equal("https://db.example/users/x.json?shallow=true", fake.Seen.Single().Url);
    }

    [Fact]
    public async Task Writes_throw_or_report_failure()
    {
        var (db, fake) = Make(r => r.Method == HttpMethod.Put ? Json("null", HttpStatusCode.Unauthorized) : Json("null"));
        await Assert.ThrowsAsync<HttpRequestException>(() => db.PutAsync("a", 1));
        Assert.False(await db.TryPutAsync("a", 1));
        Assert.True(await db.TryPatchAsync("", new Dictionary<string, object?> { ["a/b"] = null }));
        Assert.Equal("{\"a/b\":null}", fake.Seen.Last().Body);
    }

    [Fact]
    public async Task Missing_or_failed_node_reads_are_null()
    {
        var (db, _) = Make(r => r.RequestUri!.AbsolutePath.Contains("gone") ? Json("null") : Json("x", HttpStatusCode.InternalServerError));
        Assert.Null(await db.TryGetNodeAsync("gone"));
        Assert.Null(await db.TryGetNodeAsync("broken"));
    }

    [Fact]
    public async Task Etag_guards_a_write()
    {
        var (db, fake) = Make(r =>
        {
            if (r.Method == HttpMethod.Get)
            {
                var res = Json("{\"userKey\":\"sara\"}");
                res.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"abc\"");
                return res;
            }
            return Json("null", HttpStatusCode.PreconditionFailed);
        });
        var (value, etag) = await db.GetWithETagAsync<UsernameReservation>("usernames/sara");
        Assert.Equal("sara", value!.UserKey);
        Assert.False(await db.TryPutIfMatchAsync("usernames/sara", value, etag));
        Assert.Equal("\"abc\"", fake.Seen.Last().IfMatch);
    }
}
