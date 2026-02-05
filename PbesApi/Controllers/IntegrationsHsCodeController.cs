using Microsoft.AspNetCore.Mvc;
using PbesApi.Models;
using PbesApi.Services;

namespace PbesApi.Controllers;

[ApiController]
[Route("api/integrations/hscode")]
public class IntegrationsHsCodeController : ControllerBase
{
    private readonly IOllamaClient _ollamaClient;
    private readonly HsCodeScanStore _scanStore;
    private readonly OllamaOptions _options;

    public IntegrationsHsCodeController(
        IOllamaClient ollamaClient,
        HsCodeScanStore scanStore,
        Microsoft.Extensions.Options.IOptions<OllamaOptions> options)
    {
        _ollamaClient = ollamaClient;
        _scanStore = scanStore;
        _options = options.Value;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] IntegrationHsCodeScanRequest request, CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim();
        var imageBase64 = HsCodeValidation.NormalizeImageBase64(request.ImageBase64);

        if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(imageBase64))
        {
            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "rejected",
                null,
                null,
                "Provide a detailed description or image to begin."));
        }

        if (!string.IsNullOrWhiteSpace(description) && !HsCodeValidation.IsGoodsRelated(description))
        {
            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "rejected",
                null,
                null,
                "This tool only supports HS code classification for goods. Please provide a specific item description."));
        }

        if (!string.IsNullOrWhiteSpace(description) && !HsCodeValidation.IsDescriptionSpecific(description))
        {
            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "needs_more_detail",
                null,
                null,
                "Please provide a more specific description (material, use, size, brand, etc.)."));
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var modelResponse = await _ollamaClient.ScanAsync(description, imageBase64, linkedCts.Token);

            if (modelResponse.Matches.Count > 0)
            {
                var top = modelResponse.Matches[0];
                _scanStore.Add(new RecentHsCodeEntry(top.HsCode, top.Description));
            }

            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "completed",
                modelResponse.Matches,
                modelResponse.Note,
                null));
        }
        catch (OperationCanceledException)
        {
            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "failed",
                null,
                null,
                "HS code scan timed out. Try a shorter description or smaller image."));
        }
        catch (Exception)
        {
            return Ok(new IntegrationHsCodeScanResponse(
                request.RequestId,
                "failed",
                null,
                null,
                "HS code scan failed."));
        }
    }
}

public record IntegrationHsCodeScanRequest(
    string? RequestId,
    string? Description,
    string? ImageBase64,
    string? SourceSystem);

public record IntegrationHsCodeScanResponse(
    string? RequestId,
    string Status,
    List<HsCodeMatch>? Matches,
    string? Note,
    string? Message);
