using AIInterviewPlatform.Api.Auth;
using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIInterviewPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly ITokenService _tokens;

    public SessionsController(ISessionService sessions, ITokenService tokens)
    {
        _sessions = sessions;
        _tokens = tokens;
    }

    [HttpPost("start")]
    public async Task<ActionResult<SessionView>> Start(StartSessionRequest request)
        => Ok(await _sessions.StartAsync(UserId, request));

    [HttpPost("{id}/repeat")]
    public async Task<ActionResult<SessionView>> Repeat(string id)
        => Ok(await _sessions.RepeatAsync(UserId, id));

    [HttpGet]
    public async Task<ActionResult<List<SessionView>>> List()
        => Ok(await _sessions.ListAsync(UserId));

    [HttpGet("{id}")]
    public async Task<ActionResult<SessionView>> Get(string id)
        => Ok(await _sessions.GetAsync(UserId, id));

    [HttpPost("{id}/answers/{index}")]
    public async Task<ActionResult<SessionQuestionView>> SubmitAnswer(string id, int index, SubmitAnswerRequest request)
        => Ok(await _sessions.SubmitAnswerAsync(UserId, id, index, request));

    [HttpPost("{id}/finish")]
    public async Task<ActionResult<SessionView>> Finish(string id, [FromQuery] bool timedOut = false)
        => Ok(await _sessions.FinishAsync(UserId, id, timedOut));

    private string UserId => _tokens.GetUserId(User) ?? throw new AppException("Not authenticated.", 401);
}