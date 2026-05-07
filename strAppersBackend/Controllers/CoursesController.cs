using Microsoft.AspNetCore.Mvc;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ICourseBoardBuilderService _courseBoardBuilderService;

    public CoursesController(ICourseBoardBuilderService courseBoardBuilderService)
    {
        _courseBoardBuilderService = courseBoardBuilderService;
    }

    [HttpPost("use/build")]
    public async Task<ActionResult<CourseBoardBuildResponse>> Build([FromBody] CourseBoardBuildRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _courseBoardBuilderService.BuildAsync(request);
        if (!result.Success)
        {
            if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(result);
            }

            if (result.Message.Contains("already exists for this project", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(result);
            }

            return BadRequest(result);
        }

        return Ok(result);
    }
}
