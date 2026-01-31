using PbesApi.Models;

namespace PbesApi.Services;

public interface IOllamaClient
{
    Task<HsCodeModelResponse> ScanAsync(string? description, string? imageBase64, CancellationToken cancellationToken);
}
