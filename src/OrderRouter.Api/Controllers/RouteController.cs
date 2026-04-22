using Microsoft.AspNetCore.Mvc;
using OrderRouter.Api.Models;
using OrderRouter.Api.Services;

namespace OrderRouter.Api.Controllers;

/// <summary>
/// RouteController handles incoming API requests related to order routing. 
/// It exposes endpoints for routing orders and performing health checks. 
/// The controller relies on an IRoutingEngine implementation to process routing logic based on the provided order data. 
/// All responses are returned in JSON format, with the main routing endpoint always returning HTTP 200 and 
/// indicating success or failure through the 'feasible' field in the response body.
/// </summary>
/// <param name="engine"></param>
[ApiController]
[Route("api")]
public class RouteController(IRoutingEngine engine) : ControllerBase
{
    private readonly IRoutingEngine _engine = engine;

    /// <summary>
    /// Route a multi-item order to one or more suppliers.
    /// Always returns HTTP 200; check the 'feasible' field for success/failure.
    /// </summary>
    [HttpPost("route")]
    [Produces("application/json")]
    public IActionResult Route([FromBody] OrderRequest order)
    {
        var result = _engine.Route(order);
        return Ok(result);
    }

    /// <summary>Health check.</summary>
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}
