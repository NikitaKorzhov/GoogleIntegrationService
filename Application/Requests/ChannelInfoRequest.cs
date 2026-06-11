using MediatR;

namespace GoogleIntegrationService.Application.Requests
{
    public record ChannelInfoRequest(string AccessToken, string ChannelId)
        : IRequest<ChannelInfoDto?>;
}
