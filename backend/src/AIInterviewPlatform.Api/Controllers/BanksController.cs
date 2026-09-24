using AIInterviewPlatform.Api.Contracts;
using AIInterviewPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIInterviewPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BanksController : ControllerBase
{
    private readonly IQuestionService _questions;

    public BanksController(IQuestionService questions) => _questions = questions;

    [HttpGet]
    public async Task<ActionResult<List<BankSummary>>> List()
        => Ok(await _questions.ListBanksAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<BankDetail>> Get(string id)
        => Ok(await _questions.GetBankAsync(id));
}