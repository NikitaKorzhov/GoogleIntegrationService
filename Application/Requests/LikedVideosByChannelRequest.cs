using MediatR;

namespace GoogleIntegrationService.Application.Requests
{
    public record LikedVideosByChannelRequest(string AccessToken)
        : IRequest<IReadOnlyList<ChannelLikesGroupDto>>;
}
