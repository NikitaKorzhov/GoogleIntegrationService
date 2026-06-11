using GoogleIntegrationService.Application.Requests;
using GoogleIntegrationService.Infrastructure.Google;
using MediatR;

namespace GoogleIntegrationService.Application.Handlers
{
    public class LikedVideosByChannelHandler
        : IRequestHandler<LikedVideosByChannelRequest, IReadOnlyList<ChannelLikesGroupDto>>
    {
        private readonly IYouTubeUserService _youtube;

        public LikedVideosByChannelHandler(IYouTubeUserService youtube)
        {
            _youtube = youtube;
        }

        public async Task<IReadOnlyList<ChannelLikesGroupDto>> Handle(
            LikedVideosByChannelRequest request,
            CancellationToken cancellationToken)
        {
            return await _youtube.GetLikedVideosGroupedByChannelAsync(
                request.AccessToken, cancellationToken);
        }
    }
}
