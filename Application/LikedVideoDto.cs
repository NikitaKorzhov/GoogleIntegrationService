namespace GoogleIntegrationService.Application
{
    public record LikedVideoDto(
        string? Id,
        string? Title,
        string? ChannelTitle,
        string? ChannelId,
        string? Description,
        string? ThumbnailUrl,
        DateTimeOffset? PublishedAt,
        string? Duration,
        string Url);
}
