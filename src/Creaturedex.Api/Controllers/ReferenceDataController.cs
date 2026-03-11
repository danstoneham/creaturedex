namespace Creaturedex.Api.Controllers;

using Creaturedex.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/reference")]
public class ReferenceDataController(ReferenceDataRepository referenceRepo) : ControllerBase
{
    [HttpGet("colours")]
    public async Task<IActionResult> GetColours() =>
        Ok(await referenceRepo.GetColoursAsync());

    [HttpGet("tags")]
    public async Task<IActionResult> GetTags() =>
        Ok(await referenceRepo.GetTagsAsync());

    [HttpGet("habitat-types")]
    public async Task<IActionResult> GetHabitatTypes() =>
        Ok(await referenceRepo.GetHabitatTypesAsync());

    [HttpGet("diet-types")]
    public async Task<IActionResult> GetDietTypes() =>
        Ok(await referenceRepo.GetDietTypesAsync());

    [HttpGet("activity-patterns")]
    public async Task<IActionResult> GetActivityPatterns() =>
        Ok(await referenceRepo.GetActivityPatternsAsync());

    [HttpGet("conservation-statuses")]
    public async Task<IActionResult> GetConservationStatuses() =>
        Ok(await referenceRepo.GetConservationStatusesAsync());

    [HttpGet("domestication-statuses")]
    public async Task<IActionResult> GetDomesticationStatuses() =>
        Ok(await referenceRepo.GetDomesticationStatusesAsync());
}
