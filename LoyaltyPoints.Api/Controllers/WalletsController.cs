using LoyaltyPoints.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyPoints.Api.Controllers;

[ApiController]
[Route("wallets")]
public sealed class WalletsController : ControllerBase
{
    private readonly AwardService _awardService;

    public WalletsController(AwardService awardService)
    {
        _awardService = awardService;
    }

    [HttpGet("{walletId}")]
    public IActionResult GetWallet(string walletId)
    {
        var wallet = _awardService.GetWallet(walletId);
        return wallet is null
            ? NotFound(new { error = "The requested wallet was not found." })
            : Ok(wallet);
    }
}
