using LoyaltyPoints.Api.DTOs;
using LoyaltyPoints.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyPoints.Api.Controllers;

[ApiController]
[Route("awards")]
public sealed class AwardsController : ControllerBase
{
    private readonly AwardService _awardService;

    public AwardsController(AwardService awardService)
    {
        _awardService = awardService;
    }

    [HttpPost]
    public IActionResult CreateAward([FromBody] AwardRequest request)
    {
        var result = _awardService.Award(request);
        return result.Status switch
        {
            AwardResultStatus.Created => StatusCode(StatusCodes.Status201Created, ToResponse(result.Award!)),
            AwardResultStatus.AlreadyExists => Ok(ToResponse(result.Award!)),
            AwardResultStatus.Conflict => Conflict(new { error = "The award identity conflicts with an existing award." }),
            AwardResultStatus.WalletNotFound => NotFound(new { error = "The requested wallet was not found." }),
            AwardResultStatus.ValidationFailed => BadRequest(new { errors = result.Errors }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static AwardResponse ToResponse(Models.Award award) => new()
    {
        AwardId = award.AwardId,
        SourceId = award.SourceId,
        ExternalTransactionId = award.ExternalTransactionId,
        WalletId = award.WalletId,
        Points = award.Points
    };
}
