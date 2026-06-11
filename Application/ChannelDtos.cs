namespace GoogleIntegrationService.Application
{
    /// <summary>Повна інформація про канал.</summary>
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

    /// <summary>Коротке посилання на відео всередині групи каналу.</summary>
    public record LikedVideoRefDto(
        string? Name,
        string? Url);

    /// <summary>Група лайкнутих відео одного каналу з часткою від усіх лайків.</summary>
    public record ChannelLikesGroupDto(
        string? ChannelName,
        string? ChannelId,
        string? ChannelDescription,
        string? ChannelAvatarUrl,
        double Percent,
        IReadOnlyList<LikedVideoRefDto> Videos);
}
