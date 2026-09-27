using GoogleIntegrationService.Application;
using GoogleIntegrationService.Application.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/youtube")]
public class YouTubeController : ControllerBase
{
    private readonly IMediator _mediator;

    public YouTubeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Accepts the user's OAuth access token from the frontend and returns liked
    /// videos grouped by channel, with each channel's share of all the likes.
    /// </summary>
    [HttpPost("liked")]
    public async Task<ActionResult<IReadOnlyList<ChannelLikesGroupDto>>> Liked([FromBody] TokenReqDTO body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Token))
            return BadRequest("Token is required.");

        var groups = await _mediator.Send(new LikedVideosByChannelRequest(body.Token));
        return Ok(groups);
    }

    /// <summary>
    /// Accepts a channel id (in the route) and an OAuth token (in the body) and
    /// returns full information about the channel.
    /// </summary>
    [HttpPost("channel/{channelId}")]
    public async Task<ActionResult<ChannelInfoDto>> Channel(string channelId, [FromBody] TokenReqDTO body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Token))
            return BadRequest("Token is required.");

        var info = await _mediator.Send(new ChannelInfoRequest(body.Token, channelId));
        return info is null ? NotFound() : Ok(info);
    }
}
