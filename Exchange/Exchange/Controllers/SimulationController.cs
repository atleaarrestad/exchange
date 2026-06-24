using Exchange.Configuration;
using Exchange.Contracts;
using Exchange.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace Exchange.Controllers;

[ApiController]
[Route("api/admin/simulation")]
public sealed class SimulationController(
    IConfiguration configuration,
    IBrokeredBuySimulationRunner brokeredBuySimulationRunner) : ControllerBase
{
    [HttpPost("brokered-buy/reset")]
    public async Task<ActionResult<ResetBrokeredBuySimulationDataResponse>> ResetBrokeredBuyDataAsync(
        CancellationToken cancellationToken)
    {
        var simulationEnabled = configuration
            .GetSection(ConfigurationKeys.SimulationSection)
            .GetValue<bool>(ConfigurationKeys.Enabled);
        if (!simulationEnabled)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Simulation mode disabled",
                Detail = "Enable Simulation:Enabled before resetting simulation data.",
                Type = "https://httpstatuses.com/409"
            });
        }

        var response = await brokeredBuySimulationRunner.ResetDataAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("brokered-buy/start")]
    public async Task<ActionResult<StartBrokeredBuySimulationResponse>> StartBrokeredBuyAsync(
        [FromBody] StartBrokeredBuySimulationRequest request,
        CancellationToken cancellationToken)
    {
        var simulationEnabled = configuration
            .GetSection(ConfigurationKeys.SimulationSection)
            .GetValue<bool>(ConfigurationKeys.Enabled);
        if (!simulationEnabled)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Simulation mode disabled",
                Detail = "Enable Simulation:Enabled before starting a simulation run.",
                Type = "https://httpstatuses.com/409"
            });
        }

        var response = await brokeredBuySimulationRunner.StartAsync(request, cancellationToken);
        return Ok(response);
    }
}
