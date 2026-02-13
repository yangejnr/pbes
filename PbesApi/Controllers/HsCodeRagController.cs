using Microsoft.AspNetCore.Mvc;
using PbesApi.Models;
using PbesApi.Services;

namespace PbesApi.Controllers;

[ApiController]
[Route("api/hscode/rag")]
[Route("api/integrations/hscode/rag")]
public class HsCodeRagController : ControllerBase
{
    private readonly HsCodeRagService _ragService;

    public HsCodeRagController(HsCodeRagService ragService)
    {
        _ragService = ragService;
    }

    [HttpPost("reload")]
    public IActionResult Reload()
    {
        var result = _ragService.Reload();
        if (!result.Loaded)
        {
            return BadRequest(new { status = "error", message = result.Message, rows = result.RowCount });
        }

        return Ok(new { status = "ok", message = result.Message, rows = result.RowCount });
    }

    [HttpGet("lookup/{hsCode}")]
    public IActionResult Lookup(string hsCode)
    {
        var row = _ragService.LookupByHsCode(hsCode);
        if (row is null)
        {
            return NotFound(new { status = "not_found", message = "No row found for the provided HS code." });
        }

        return Ok(row);
    }

    [HttpPost("search")]
    public IActionResult Search([FromBody] HsCodeRagSearchRequest request)
    {
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return BadRequest(new { status = "error", message = "Query is required." });
        }

        var response = _ragService.Search(query, request.TopK);
        return Ok(response);
    }
}
