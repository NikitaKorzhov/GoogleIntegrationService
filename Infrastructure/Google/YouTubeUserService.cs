using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using GoogleIntegrationService.Application;

namespace GoogleIntegrationService.Infrastructure.Google
{
    /// <summary>
    /// Працює від імені користувача за OAuth access-токеном, який приходить із фронтенду.
    /// Не залежить від серверної авторизації (singleton YouTubeService).
    /// </summary>
    public interface IYouTubeUserService
    {
        Task<IReadOnlyList<LikedVideoDto>> GetLikedVideosAsync(
            string accessToken,
            CancellationToken cancellationToken = default);

        Task<ChannelInfoDto?> GetChannelInfoAsync(
            string accessToken,
            string channelId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ChannelLikesGroupDto>> GetLikedVideosGroupedByChannelAsync(
            string accessToken,
            CancellationToken cancellationToken = default);
    }

    public class YouTubeUserService : IYouTubeUserService
    {
        private readonly string _applicationName;

        public YouTubeUserService(IConfiguration config)
        {
            _applicationName = config["Google:ApplicationName"] ?? "GoogleIntegrationService";
        }

        public async Task<IReadOnlyList<LikedVideoDto>> GetLikedVideosAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token is required.", nameof(accessToken));

            using var youtube = BuildService(accessToken);

            var liked = new List<LikedVideoDto>();
            string? pageToken = null;

            // YouTube повертає максимум 50 елементів на сторінку — пагінуємо, доки є NextPageToken.
            do
            {
                var request = youtube.PlaylistItems.List("snippet,contentDetails");
                request.PlaylistId = "LL"; // Liked videos playlist
                request.MaxResults = 50;
                request.PageToken = pageToken;

                var response = await GoogleApiRetry.ExecuteAsync(
                    ct => request.ExecuteAsync(ct), cancellationToken);

                foreach (var item in response.Items)
                {
                    var snippet = item.Snippet;
                    var videoId = item.ContentDetails?.VideoId;

                    var thumbnail =
                        snippet?.Thumbnails?.Medium?.Url ??
                        snippet?.Thumbnails?.High?.Url ??
                        snippet?.Thumbnails?.Default__?.Url;

                    liked.Add(new LikedVideoDto(
                        Id: videoId,
                        Title: snippet?.Title,
                        ChannelTitle: snippet?.VideoOwnerChannelTitle,
                        ChannelId: snippet?.VideoOwnerChannelId,
                        Description: snippet?.Description,
                        ThumbnailUrl: thumbnail,
                        PublishedAt: snippet?.PublishedAtDateTimeOffset,
                        Duration: null, // duration треба брати окремо, якщо потрібно
                        Url: $"https://www.youtube.com/watch?v={videoId}"
                    ));
                }

                pageToken = response.NextPageToken;

            } while (!string.IsNullOrEmpty(pageToken) && !cancellationToken.IsCancellationRequested);

            return liked;
        }

        /// <summary>Приймає id каналу і повертає повну інформацію про нього.</summary>
        public async Task<ChannelInfoDto?> GetChannelInfoAsync(
            string accessToken,
            string channelId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("Access token is required.", nameof(accessToken));
            if (string.IsNullOrWhiteSpace(channelId))
                throw new ArgumentException("Channel id is required.", nameof(channelId));

            using var youtube = BuildService(accessToken);

            var request = youtube.Channels.List("snippet,statistics");
            request.Id = new Repeatable<string>(new[] { channelId });

            var response = await GoogleApiRetry.ExecuteAsync(
                ct => request.ExecuteAsync(ct), cancellationToken);

            var channel = response.Items?.FirstOrDefault();
            return channel is null ? null : MapChannel(channel);
        }

        /// <summary>
        /// Пробігається по лайкнутих відео й групує їх за каналами,
        /// додаючи частку (percent) кожного каналу від усіх лайків.
        /// </summary>
        public async Task<IReadOnlyList<ChannelLikesGroupDto>> GetLikedVideosGroupedByChannelAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            var liked = await GetLikedVideosAsync(accessToken, cancellationToken);
            if (liked.Count == 0)
                return Array.Empty<ChannelLikesGroupDto>();

            var total = liked.Count;

            using var youtube = BuildService(accessToken);

            // Дотягуємо опис/назву каналів одним батчем (до 50 id за запит).
            var channelIds = liked
                .Select(v => v.ChannelId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .Distinct();

            var channels = await GetChannelsAsync(youtube, channelIds, cancellationToken);

            return liked
                .GroupBy(v => v.ChannelId ?? string.Empty)
                .Select(g =>
                {
                    channels.TryGetValue(g.Key, out var info);
                    var sample = g.First();

                    return new ChannelLikesGroupDto(
                        ChannelName: info?.Title ?? sample.ChannelTitle,
                        ChannelId: string.IsNullOrEmpty(g.Key) ? sample.ChannelId : g.Key,
                        ChannelDescription: info?.Description,
                        ChannelAvatarUrl: info?.ThumbnailUrl,
                        Percent: Math.Round((double)g.Count() / total * 100, 2),
                        Videos: g.Select(v => new LikedVideoRefDto(v.Title, v.Url)).ToList());
                })
                .OrderByDescending(x => x.Percent)
                .ToList();
        }

        private YouTubeService BuildService(string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);
            return new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _applicationName
            });
        }

        private static async Task<Dictionary<string, ChannelInfoDto>> GetChannelsAsync(
            YouTubeService youtube,
            IEnumerable<string> channelIds,
            CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, ChannelInfoDto>();

            foreach (var chunk in channelIds.Chunk(50))
            {
                var request = youtube.Channels.List("snippet,statistics");
                request.Id = new Repeatable<string>(chunk);
                request.MaxResults = 50;

                var response = await GoogleApiRetry.ExecuteAsync(
                    ct => request.ExecuteAsync(ct), cancellationToken);

                if (response.Items is null) continue;

                foreach (var channel in response.Items)
                {
                    if (channel.Id is not null)
                        map[channel.Id] = MapChannel(channel);
                }
            }

            return map;
        }

        private static ChannelInfoDto MapChannel(Channel channel)
        {
            var snippet = channel.Snippet;
            var statistics = channel.Statistics;

            var thumbnail =
                snippet?.Thumbnails?.High?.Url ??
                snippet?.Thumbnails?.Medium?.Url ??
                snippet?.Thumbnails?.Default__?.Url;

            DateTimeOffset? publishedAt =
                DateTimeOffset.TryParse(snippet?.PublishedAtRaw, out var parsed) ? parsed : null;

            return new ChannelInfoDto(
                Id: channel.Id,
                Title: snippet?.Title,
                Description: snippet?.Description,
                CustomUrl: snippet?.CustomUrl,
                Country: snippet?.Country,
                PublishedAt: publishedAt,
                ThumbnailUrl: thumbnail,
                SubscriberCount: statistics?.SubscriberCount,
                VideoCount: statistics?.VideoCount,
                ViewCount: statistics?.ViewCount,
                Url: channel.Id is null ? null : $"https://www.youtube.com/channel/{channel.Id}");
        }
    }
}
