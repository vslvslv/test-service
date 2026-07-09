using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestService.Api.Models;
using TestService.Api.Services;

namespace TestService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InfoController : ControllerBase
{
    private readonly IAppInfoService _appInfoService;

    public InfoController(IAppInfoService appInfoService)
    {
        _appInfoService = appInfoService;
    }

    /// <summary>
    /// Get version and runtime information about the running service.
    /// Requires authentication; returns only non-sensitive fields for display in the UI.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AppInfo), StatusCodes.Status200OK)]
    public ActionResult<AppInfo> GetInfo() => Ok(_appInfoService.GetInfo());
}
