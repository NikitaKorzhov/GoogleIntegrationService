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
    /// Приймає OAuth access-токен користувача з фронтенду й повертає лайкнуті відео,
    /// згруповані за каналами, з часткою кожного каналу від усіх лайків.
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
    /// Приймає id каналу (у маршруті) та OAuth-токен (у тілі) і повертає
    /// повну інформацію про канал.
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
