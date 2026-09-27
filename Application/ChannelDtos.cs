namespace GoogleIntegrationService.Application
{
    /// <summary>Full information about a channel.</summary>
    public record ChannelInfoDto(
        string? Id,
        string? Title,
        string? Description,
        string? CustomUrl,
        string? Country,
        DateTimeOffset? PublishedAt,
        string? ThumbnailUrl,
        ulong? SubscriberCount,
        ulong? VideoCount,
        ulong? ViewCount,
        string? Url);

    /// <summary>A short reference to a video within a channel's group.</summary>
    public record LikedVideoRefDto(
        string? Name,
        string? Url);

    /// <summary>A group of liked videos from a single channel with its share of all likes.</summary>
    public record ChannelLikesGroupDto(
        string? ChannelName,
        string? ChannelId,
        string? ChannelDescription,
        string? ChannelAvatarUrl,
        double Percent,
        IReadOnlyList<LikedVideoRefDto> Videos);
}
