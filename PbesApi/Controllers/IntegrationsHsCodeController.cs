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
    private readonly HsCodeMatchEnrichmentService _enrichmentService;
    private readonly OllamaOptions _options;

    public IntegrationsHsCodeController(
        IOllamaClient ollamaClient,
        HsCodeScanStore scanStore,
        HsCodeMatchEnrichmentService enrichmentService,
        Microsoft.Extensions.Options.IOptions<OllamaOptions> options)
    {
        _ollamaClient = ollamaClient;
        _scanStore = scanStore;
        _enrichmentService = enrichmentService;
        _options = options.Value;
    }

    [HttpPost("scan")]
    public IActionResult Scan([FromBody] IntegrationHsCodeScanRequest request)
    {
        var description = request.Description?.Trim();
        var imageBase64 = HsCodeValidation.NormalizeImageBase64(request.ImageBase64);

        if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(imageBase64))
        {
            return Ok(new IntegrationHsCodeScanStatusResponse(
                request.RequestId,
                "rejected",
                null,
                null,
                null,
                "Provide a detailed description or image to begin."));
        }

        if (!string.IsNullOrWhiteSpace(description) && !HsCodeValidation.IsGoodsRelated(description))
        {
            return Ok(new IntegrationHsCodeScanStatusResponse(
                request.RequestId,
                "rejected",
                null,
                null,
                null,
                "This tool only supports HS code classification for goods. Please provide a specific item description."));
        }

        if (!string.IsNullOrWhiteSpace(description) && !HsCodeValidation.IsDescriptionSpecific(description))
        {
            return Ok(new IntegrationHsCodeScanStatusResponse(
                request.RequestId,
                "needs_more_detail",
                null,
                null,
                null,
                "Please provide a more specific description (material, use, size, brand, etc.)."));
        }

        var job = _scanStore.CreateJob(request.RequestId);

        _ = Task.Run(async () =>
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                var modelResponse = await _ollamaClient.ScanAsync(description, imageBase64, timeoutCts.Token);
                var enrichedMatches = _enrichmentService.Enrich(modelResponse.Matches, description);

                if (enrichedMatches.Count > 0)
                {
                    var top = enrichedMatches[0];
                    _scanStore.Add(new RecentHsCodeEntry(top.HsCode, top.Description));
                }

                var response = new HsCodeScanResponse(
                    enrichedMatches,
                    modelResponse.Note,
                    _scanStore.GetRecent());

                _scanStore.CompleteJob(job.Id, response);
            }
            catch (OperationCanceledException)
            {
                _scanStore.FailJob(job.Id, "HS code scan timed out. Try a shorter description or smaller image.");
            }
            catch (Exception)
            {
                _scanStore.FailJob(job.Id, "HS code scan failed.");
            }
        });

        return Accepted(new IntegrationHsCodeScanStartResponse(request.RequestId, "accepted", job.Id));
    }

    [HttpGet("scan/{jobId:guid}")]
    public IActionResult GetScanStatus(Guid jobId)
    {
        if (!_scanStore.TryGetJob(jobId, out var job))
        {
            return NotFound("Scan job not found.");
        }

        if (job.Status == HsCodeScanJobStatus.Completed)
        {
            return Ok(new IntegrationHsCodeScanStatusResponse(
                job.RequestId,
                "completed",
                job.Id,
                job.Result?.Matches,
                job.Result?.Note,
                null));
        }

        if (job.Status == HsCodeScanJobStatus.Failed)
        {
            return Ok(new IntegrationHsCodeScanStatusResponse(
                job.RequestId,
                "failed",
                job.Id,
                null,
                null,
                job.Error));
        }

        return Ok(new IntegrationHsCodeScanStatusResponse(
            job.RequestId,
            "pending",
            job.Id,
            null,
            null,
            null));
    }
}

public record IntegrationHsCodeScanRequest(
    string? RequestId,
    string? Description,
    string? ImageBase64,
    string? SourceSystem);

public record IntegrationHsCodeScanStartResponse(
    string? RequestId,
    string Status,
    Guid JobId);

public record IntegrationHsCodeScanStatusResponse(
    string? RequestId,
    string Status,
    Guid? JobId,
    List<HsCodeMatch>? Matches,
    string? Note,
    string? Message);
