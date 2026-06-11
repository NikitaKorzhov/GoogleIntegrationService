using GoogleIntegrationService.Application.Requests;
using GoogleIntegrationService.Infrastructure.Google;
using MediatR;

namespace GoogleIntegrationService.Application.Handlers
{
    public class ChannelInfoHandler
        : IRequestHandler<ChannelInfoRequest, ChannelInfoDto?>
    {
        private readonly IYouTubeUserService _youtube;

        public ChannelInfoHandler(IYouTubeUserService youtube)
        {
            _youtube = youtube;
        }

        public async Task<ChannelInfoDto?> Handle(
            ChannelInfoRequest request,
            CancellationToken cancellationToken)
        {
            return await _youtube.GetChannelInfoAsync(
                request.AccessToken, request.ChannelId, cancellationToken);
        }
    }
}
