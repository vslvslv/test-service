using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestService.Api.Emulators.Praxis;
using TestService.Api.Emulators.Transactions;
using TestService.Api.Models;

namespace TestService.Api.Controllers;

/// <summary>Configuration (merchants) and assertions (transactions) of the provider emulators.</summary>
[ApiController]
[Route("api/emulators")]
[Authorize]
public class EmulatorsController : ControllerBase
{
    private readonly IPraxisMerchantRepository _merchants;
    private readonly ITransactionRepository _transactions;
    private readonly PraxisEmulator _praxis;
    private readonly ILogger<EmulatorsController> _logger;

    public EmulatorsController(IPraxisMerchantRepository merchants, ITransactionRepository transactions, PraxisEmulator praxis,
        ILogger<EmulatorsController> logger)
    {
        _merchants = merchants;
        _transactions = transactions;
        _praxis = praxis;
        _logger = logger;
    }

    [HttpGet("praxis/merchants")]
    [Authorize(Policy = PermissionDefinitions.MocksRead)]
    public async Task<ActionResult<IEnumerable<PraxisMerchantView>>> GetMerchants([FromQuery] string? environment = null)
    {
        try
        {
            var merchants = await _merchants.ListAsync(environment);
            return Ok(merchants.Select(PraxisMerchantView.From));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Praxis emulator merchants");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("praxis/merchants")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult<PraxisMerchantView>> CreateMerchant([FromBody] PraxisMerchantRequest? request)
    {
        try
        {
            var (merchant, error) = PraxisMerchantValidator.Validate(request);
            if (merchant is null)
            {
                return BadRequest(new { message = error });
            }

            var created = await _merchants.CreateAsync(merchant);
            return CreatedAtAction(nameof(GetMerchants), new { environment = created.Environment }, PraxisMerchantView.From(created));
        }
        catch (DuplicateMerchantException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Praxis emulator merchant");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("praxis/merchants/{id}")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult<PraxisMerchantView>> UpdateMerchant(string id, [FromBody] PraxisMerchantRequest? request)
    {
        try
        {
            var existing = await _merchants.GetByIdAsync(id);
            if (existing is null)
            {
                return NotFound(new { message = $"Merchant '{id}' not found." });
            }

            var (merchant, error) = PraxisMerchantValidator.Validate(request, existing);
            if (merchant is null)
            {
                return BadRequest(new { message = error });
            }

            if (!await _merchants.UpdateAsync(merchant))
            {
                return NotFound(new { message = $"Merchant '{id}' not found." });
            }
            return Ok(PraxisMerchantView.From(merchant));
        }
        catch (DuplicateMerchantException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Praxis emulator merchant {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("praxis/merchants/{id}")]
    [Authorize(Policy = PermissionDefinitions.MocksWrite)]
    public async Task<ActionResult> DeleteMerchant(string id)
    {
        try
        {
            return await _merchants.DeleteAsync(id) ? NoContent() : NotFound(new { message = $"Merchant '{id}' not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Praxis emulator merchant {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("praxis/transactions")]
    [Authorize(Policy = PermissionDefinitions.MocksRead)]
    public async Task<ActionResult<IEnumerable<EmulatedTransaction>>> GetTransactions(
        [FromQuery] string? environment = null, [FromQuery] string? orderId = null, [FromQuery] long? tid = null)
    {
        try
        {
            // Due deadlines first, so the read shows them as find-transaction would.
            await _praxis.ApplyDueDeadlinesAsync();
            return Ok(await _transactions.QueryAsync(environment, orderId, tid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Praxis emulator transactions");
            return StatusCode(500, "Internal server error");
        }
    }
}
