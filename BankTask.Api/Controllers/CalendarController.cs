
using System.Security.Claims;
using BankTask.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankTask.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly IGoogleCalendarService _googleCalendarService;

    public CalendarController(
        IGoogleCalendarService googleCalendarService)
    {
        _googleCalendarService = googleCalendarService;
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEvents()
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new
            {
                Message = "INVALID_USER_ID"
            });
        }

        var events =
            await _googleCalendarService.GetEventsAsync(userId);

        return Content(
            events,
            "application/json");
    }
}
