using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mizon.API;
using Mizon.API.Client.Operations.Clients;
using Mizon.API.Client.Operations.Media;
using Mizon.API.Client.Operations.Musics;
using Mizon.API.Client.Operations.Search;
using Mizon.API.Client.Operations.Stories;
using Mizon.API.Client.Requests.Stories;

internal static class OperationChecks
{
    private const string PublicId = "0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a72";
    private const string OtherPublicId = "0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a73";
    private static readonly Guid Id = Guid.Parse(PublicId);
    private static readonly Guid OtherId = Guid.Parse(OtherPublicId);

    public static async Task RunAsync()
    {
        Ensure(!JsonSerializer.IsReflectionEnabledByDefault, "Run these checks with JsonSerializerIsReflectionEnabledByDefault=false.");
        using var handler = new OperationHandler();
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var api = new MizonApi(httpClient, cache);
        api.SetToken("sdk-diagnostics-token");

        await CheckAsync(api, handler, new LoginOperation(new() { Username = "sample", Password = "test" }, updateToken: false), "/Clients/Login", "POST", authorized: false);
        await CheckAsync(api, handler, new RegisterOperation(new() { Username = "sample", Password = "test", FirstName = "نام", LastName = "نمونه", EmailAddress = "sample@example.test" }, updateToken: false), "/Clients/Register", "POST", authorized: false);
        await CheckAsync(api, handler, new LogoutOperation(new()), "/Clients/Logout", "POST");
        await CheckAsync(api, handler, new LogoutOperation(new()), "/Clients/Logout", "POST");
        await CheckAsync(api, handler, new MeOperation(new()), "/Clients/Me", "GET");
        await CheckAsync(api, handler, new ClientMoodTypesOperation(new()), "/Clients/ClientMoodTypes", "GET");
        await CheckAsync(api, handler, new ClientMoodOperation(new() { ClientMoodTypePublicId = Id }), "/Clients/ClientMood", "POST");
        await CheckAsync(api, handler, new ClientProfileOperation(new() { ClientPublicId = Id }), "/Clients/ClientProfile", "GET", $"?ClientPublicId={PublicId}");
        await CheckAsync(api, handler, new FollowingActivitiesOperation(new() { Scope = "music", Cursor = "next", PageSize = 4 }), "/Clients/FollowingActivities", "POST");
        await CheckAsync(api, handler, new MediaOperation(new() { MediaPublicId = Id }), "/Medias/Media", "GET", $"?MediaPublicId={PublicId}");

        await CheckAsync(api, handler, new MusicOperation(new() { MusicPublicId = Id }), "/Musics/Music", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicFileOperation(new() { MusicPublicId = Id }), "/Musics/MusicFile", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicStemMixOperation(new() { MusicPublicId = Id }), "/Musics/MusicStemMix", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicFileWaveformOperation(new() { MusicFilePublicId = Id }), "/Musics/MusicFileWaveform", "GET", $"?MusicFilePublicId={PublicId}");
        await CheckAsync(api, handler, new MusicLyricOperation(new() { MusicPublicId = Id }), "/Musics/MusicLyric", "GET", $"?MusicPublicId={PublicId}");
        var lyric = await CheckAsync(api, handler, new MusicLyricLineOperation(new() { MusicLyricPublicId = Id }), "/Musics/MusicLyricLine", "GET", $"?MusicLyricPublicId={PublicId}",
            responseJson: """{"content":{"MusicLyricLines":[{"text":"سلام دنیا","startTimeInMilliseconds":1000,"MusicLyricLineWords":[{"Text":"سلام","StartTimeInMilliseconds":1100,"endTimeInMilliseconds":1500}]}]},"error":null}""");
        Ensure(lyric.MusicLyricLines[0].MusicLyricLineWords[0].Text == "سلام" && lyric.MusicLyricLines[0].MusicLyricLineWords[0].EndTimeInMilliseconds == 1500, "Nested lyric response/case-insensitive JSON failed.");
        await CheckAsync(api, handler, new MusicContributorsOperation(new() { MusicPublicId = Id }), "/Musics/MusicContributors", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicAlbumMusicsOperation(new() { MusicPublicId = Id }), "/Musics/MusicAlbumMusics", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicContributorMusicsOperation(new() { MusicPublicId = Id }), "/Musics/MusicContributorMusics", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicGenreTypesOperation(new()), "/Musics/MusicGenreTypes", "GET");
        await CheckAsync(api, handler, new MusicCommentsOperation(new() { MusicPublicId = Id }), "/Musics/MusicComments", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicCommentsOperation(new() { MusicPublicId = Id, MusicCommentPublicId = OtherId }), "/Musics/MusicComments", "GET", $"?MusicPublicId={PublicId}&MusicCommentPublicId={OtherPublicId}");
        await CheckAsync(api, handler, new MusicCommentCreateOperation(new() { MusicPublicId = Id, Text = "نظر آزمایشی", Timestamp = TimeSpan.FromSeconds(12.5) }), "/Musics/MusicCommentCreate", "POST",
            bodyCheck: body => Ensure(body.GetProperty("timestamp").GetString() == "00:00:12.5000000" && body.GetProperty("text").GetString() == "نظر آزمایشی", "Comment text/timestamp serialization failed."));
        await CheckAsync(api, handler, new MusicReactionsOperation(new() { MusicPublicId = Id }), "/Musics/MusicReactions", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new MusicReactionTypesOperation(new()), "/Musics/MusicReactionTypes", "GET");
        await CheckAsync(api, handler, new MusicReactionOperation(new() { MusicPublicId = Id, MusicReactionTypePublicId = OtherId, Reacted = true }), "/Musics/MusicReaction", "POST");
        await CheckAsync(api, handler, new MusicLikeOperation(new() { MusicPublicId = Id, Liked = true }), "/Musics/MusicLike", "POST");
        await CheckAsync(api, handler, new MusicViewOperation(new() { MusicPublicId = Id }), "/Musics/MusicView", "POST");
        await CheckAsync(api, handler, new MusicPlayOperation(new() { MusicPublicId = Id, PlaybackPublicId = OtherId }), "/Musics/MusicPlay", "POST");
        await CheckAsync(api, handler, new MusicFeaturesOperation(new() { Limit = 12 }), "/Musics/MusicFeatures", "POST");
        await CheckAsync(api, handler, new MusicPlaylistsOperation(new() { MusicPublicId = Id }), "/Musics/MusicPlaylists", "POST");
        await CheckAsync(api, handler, new MusicLibraryOperation(new() { PageSize = 5, Cursor = "next" }), "/Musics/MusicLibrary", "POST");
        await CheckAsync(api, handler, new MusicPlaylistDetailsOperation(new() { MusicPlaylistPublicId = Id, PageSize = 30 }), "/Musics/MusicPlaylistDetails", "POST");
        await CheckAsync(api, handler, new MusicPlaylistCreateOperation(new() { MusicPublicId = Id, Title = "آهنگ‌های من" }), "/Musics/MusicPlaylistCreate", "POST");
        await CheckAsync(api, handler, new MusicPlaylistMembershipOperation(new() { MusicPublicId = Id, MusicPlaylistPublicIds = [OtherId] }), "/Musics/MusicPlaylistMembership", "POST",
            bodyCheck: body => Ensure(body.GetProperty("musicPlaylistPublicIds")[0].GetGuid() == OtherId, "Playlist ID collection serialization failed."));

        await CheckAsync(api, handler, new StoriesOperation(), "/Stories/Stories", "GET");
        await CheckAsync(api, handler, new MusicStoriesOperation(new() { MusicPublicId = Id }), "/Stories/MusicStories", "GET", $"?MusicPublicId={PublicId}");
        await CheckAsync(api, handler, new StoryDetailsOperation(new() { StoryPublicId = Id }), "/Stories/StoryDetails", "GET", $"?StoryPublicId={PublicId}");
        await CheckAsync(api, handler, new StoryViewOperation(new() { StoryPublicId = Id }), "/Stories/StoryView", "POST");
        await CheckAsync(api, handler, new StoryLikeOperation(new() { StoryPublicId = Id, Liked = true }), "/Stories/StoryLike", "POST");
        await CheckAsync(api, handler, new StoryCreateOperation(new()
        {
            ContentMediaPublicId = Id,
            StoryContentTypePublicId = OtherId,
            Title = "استوری",
            ExpireDateTime = null,
            Widgets = [new StoryWidgetCreateRequest { MusicPublicId = Id, X = .25f, Y = .75f, Rotation = -15.5f, ZIndex = 2 }]
        }), "/Stories/StoryCreate", "POST", bodyCheck: body =>
        {
            Ensure(body.GetProperty("expireDateTime").ValueKind == JsonValueKind.Null, "Nullable story expiration changed.");
            var widget = body.GetProperty("widgets")[0];
            Ensure(widget.GetProperty("musicPublicId").GetGuid() == Id && widget.GetProperty("rotation").GetSingle() == -15.5f, "Nested story widget serialization failed.");
        });

        const string query = "آهنگ + 100% & mix/?";
        handler.Expect("/Search/Global", "GET", "?Query=" + Uri.EscapeDataString(query), authorized: true,
            """
            {"ItemType":"music","PublicId":"0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a72","Title":"آهنگ"}
            {"itemType":"client","publicId":"0195dd3c-9a3e-7c4a-8c6f-2e9c0f6b1a73","username":"sample"}
            """);
        var count = 0;
        await foreach (var item in api.SendStreamRequestAsync(new GlobalSearchOperation(new() { Query = query })))
        {
            Ensure(item.PublicId == (count == 0 ? Id : OtherId), "Streaming search item/case-insensitive JSON failed.");
            count++;
        }
        Ensure(count == 2, "Streaming search lost a result.");
        Console.WriteLine("PASS: all 40 built-in operations; GET routing, auth, request/response metadata, nested JSON, logout repeat and streamed search with reflection disabled.");
    }

    private static async Task<TResponse> CheckAsync<TRequest, TResponse>(MizonApi api, OperationHandler handler,
        MizonApiRequest<TRequest, TResponse> operation, string path, string method, string query = "", bool authorized = true,
        string? responseJson = null, Action<JsonElement>? bodyCheck = null)
        where TRequest : IApiRequest where TResponse : IApiResponse
    {
        var calls = handler.CallCount;
        handler.Expect(path, method, query, authorized, responseJson ?? (calls % 2 == 0 ? "{\"Content\":{},\"Error\":null}" : "{\"content\":{},\"error\":null}"), bodyCheck);
        var response = await api.SendRequestAsync(operation);
        Ensure(response.IsSuccess && response.Content is not null, $"{path} failed: {response.Error?.Title}: {response.Error?.Details}");
        Ensure(handler.CallCount == calls + 1, $"{path} did not reach the transport (unexpected cache hit).");
        return response.Content!;
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class OperationHandler : HttpMessageHandler
    {
        private string _path = string.Empty;
        private string _method = string.Empty;
        private string _query = string.Empty;
        private string _response = string.Empty;
        private bool _authorized;
        private Action<JsonElement>? _bodyCheck;
        public int CallCount { get; private set; }

        public void Expect(string path, string method, string query, bool authorized, string response, Action<JsonElement>? bodyCheck = null)
            => (_path, _method, _query, _authorized, _response, _bodyCheck) = (path, method, query, authorized, response, bodyCheck);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Ensure(request.RequestUri?.AbsolutePath == _path, $"Wrong route: {request.RequestUri} (expected {_path}).");
            Ensure(request.RequestUri?.Query == _query, $"Wrong query for {_path}: {request.RequestUri?.Query} (expected {_query}).");
            Ensure(request.Method.Method == _method, $"Wrong HTTP method for {_path}.");
            Ensure((request.Headers.Authorization is not null) == _authorized, $"Wrong authentication policy for {_path}.");

            if (_method == "POST")
            {
                Ensure(request.Content is not null, $"Missing request body for {_path}.");
                await using var bodyStream = await request.Content!.ReadAsStreamAsync(cancellationToken);
                using var gzip = request.Content.Headers.ContentEncoding.Contains("gzip") ? new GZipStream(bodyStream, CompressionMode.Decompress, leaveOpen: true) : null;
                using var document = await JsonDocument.ParseAsync(gzip is null ? bodyStream : gzip, cancellationToken: cancellationToken);
                var content = document.RootElement.GetProperty("content");
                Ensure(content.ValueKind == JsonValueKind.Object, $"Missing request content object for {_path}.");
                _bodyCheck?.Invoke(content);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, _path == "/Search/Global" ? "application/x-ndjson" : "application/json")
            };
        }
    }
}
