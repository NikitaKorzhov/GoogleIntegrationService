using System.Security.Claims;
using GoogleIntegrationService.Application;
using GoogleIntegrationService.Application.Requests;
using GoogleIntegrationService.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/youtube")]
public class YouTubeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserAccountService _userAccountService;

    public YouTubeController(IMediator mediator, IUserAccountService userAccountService)
    {
        _mediator = mediator;
        _userAccountService = userAccountService;
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
    /// Same as POST /api/youtube/liked, but takes no body: requires the app's own JWT
    /// (Authorization: Bearer ...), reads the Google id from it, looks up the Google
    /// access token stored for that user in the database, and uses that instead.
    /// </summary>
    [Authorize]
    [HttpGet("liked/me")]
    public async Task<ActionResult<IReadOnlyList<ChannelLikesGroupDto>>> LikedForCurrentUser()
    {
        var googleId = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(googleId))
            return Unauthorized("Token does not contain a Google id.");

        var accessToken = await _userAccountService.GetGoogleTokenAsync(googleId);
        if (string.IsNullOrWhiteSpace(accessToken))
            return NotFound("No stored Google token for this user.");

        var groups = await _mediator.Send(new LikedVideosByChannelRequest(accessToken));
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
