using Microsoft.AspNetCore.Mvc;
using PbesApi.Models;
using PbesApi.Services;

namespace PbesApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HsCodeController : ControllerBase
{
    private readonly IOllamaClient _ollamaClient;
    private readonly HsCodeScanStore _scanStore;
    private readonly OllamaOptions _options;

    public HsCodeController(IOllamaClient ollamaClient, HsCodeScanStore scanStore, Microsoft.Extensions.Options.IOptions<OllamaOptions> options)
    {
        _ollamaClient = ollamaClient;
        _scanStore = scanStore;
        _options = options.Value;
    }

    [HttpGet("recent")]
    public IActionResult GetRecent()
    {
        return Ok(_scanStore.GetRecent());
    }

    [HttpPost("scan")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Scan([FromForm] HsCodeScanRequest request, CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim();
        var file = request.File;

        if (string.IsNullOrWhiteSpace(description) && file is null)
        {
            return BadRequest("Provide a detailed description or upload a clear image to begin.");
        }

        if (!string.IsNullOrWhiteSpace(description) && !IsGoodsRelated(description))
        {
            return BadRequest("This tool only supports HS code classification for goods. Please provide a specific item description.");
        }

        if (!string.IsNullOrWhiteSpace(description) && !IsDescriptionSpecific(description))
        {
            return BadRequest("Please provide a more specific description (material, use, size, brand, etc.).");
        }

        string? imageBase64 = null;

        if (file is not null)
        {
            if (!IsAllowedFileType(file.ContentType))
            {
                return BadRequest("Only PDF, JPEG, JPG, or PNG files are allowed.");
            }

            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                if (file.Length < 80 * 1024)
                {
                    return BadRequest("Image appears too small. Upload a clearer photo with more detail.");
                }

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream, cancellationToken);
                imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }
            else if (string.IsNullOrWhiteSpace(description))
            {
                return BadRequest("PDF uploaded. Please include a detailed item description for accurate matching.");
            }
        }

        var job = _scanStore.CreateJob();

        _ = Task.Run(async () =>
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

            try
            {
                var modelResponse = await _ollamaClient.ScanAsync(description, imageBase64, linkedCts.Token);

                if (modelResponse.Matches.Count > 0)
                {
                    var top = modelResponse.Matches[0];
                    _scanStore.Add(new RecentHsCodeEntry(top.HsCode, top.Description));
                }

                var response = new HsCodeScanResponse(
                    modelResponse.Matches,
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

        return Accepted(new { jobId = job.Id });
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
            return Ok(new { status = "completed", result = job.Result });
        }

        if (job.Status == HsCodeScanJobStatus.Failed)
        {
            return Ok(new { status = "failed", error = job.Error });
        }

        return Ok(new { status = "pending" });
    }

    private static bool IsAllowedFileType(string contentType)
    {
        return contentType is "application/pdf" or "image/jpeg" or "image/png";
    }

    private static bool IsDescriptionSpecific(string description)
    {
        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 5 && description.Length >= 25;
    }

    private static bool IsGoodsRelated(string description)
    {
        var text = description.ToLowerInvariant();

        var blockedPhrases = new[]
        {
            "weather",
            "football",
            "soccer",
            "match",
            "scores",
            "news",
            "politic",
            "election",
            "president",
            "governor",
            "import duty",
            "customs duty",
            "tariff",
            "tax rate",
            "exchange rate",
            "visa",
            "passport"
        };

        foreach (var phrase in blockedPhrases)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var blockedIntents = new[]
        {
            "tell me about",
            "what is",
            "who is",
            "how to",
            "explain"
        };

        foreach (var phrase in blockedIntents)
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

public class HsCodeScanRequest
{
    public string? Description { get; set; }
    public IFormFile? File { get; set; }
}
