namespace Dynamics365ImportData.Persistence;

using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;
using Dynamics365ImportData.Sanitization;
using Dynamics365ImportData.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Text.Json;

public class JsonFileMigrationResultRepository : IMigrationResultRepository
{
    private readonly ILogger<JsonFileMigrationResultRepository> _logger;
    private readonly string _resultsDirectory;
    private readonly IResultSanitizer _sanitizer;

    public JsonFileMigrationResultRepository(
        IOptions<PersistenceSettings> persistenceSettings,
        IOptions<DestinationSettings> destinationSettings,
        IResultSanitizer sanitizer,
        ILogger<JsonFileMigrationResultRepository> logger)
    {
        _sanitizer = sanitizer;
        _logger = logger;

        var settings = persistenceSettings.Value;
        if (!string.IsNullOrWhiteSpace(settings.ResultsDirectory))
        {
            _resultsDirectory = settings.ResultsDirectory;
        }
        else
        {
            var outputDir = destinationSettings.Value.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputDir))
                outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Output");
            _resultsDirectory = Path.Combine(outputDir, "results");
        }
    }

    public async Task SaveCycleResultAsync(CycleResult result, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_resultsDirectory);

            var json = SerializeWithSanitizedErrors(result);
            var targetPath = Path.Combine(_resultsDirectory, $"cycle-{result.Timestamp:yyyy-MM-ddTHHmmss}.json");
            var tempPath = Path.Combine(_resultsDirectory, $".tmp-{Guid.NewGuid():N}.json");

            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken);
                File.Move(tempPath, targetPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }

            _logger.LogInformation("Cycle result saved to {ResultPath}", targetPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cycle result for {CycleId}", result.CycleId);
        }
    }

    public async Task<CycleResult?> GetCycleResultAsync(string cycleId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_resultsDirectory, $"{cycleId}.json");
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<CycleResult>(json, JsonDefaults.ResultJsonOptions);
    }

    public async Task<IReadOnlyList<CycleResult>> GetLatestCycleResultsAsync(int count, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resultsDirectory))
            return [];

        var files = Directory.GetFiles(_resultsDirectory, "cycle-*.json")
            .OrderDescending()
            .Take(count)
            .ToList();

        var results = new List<CycleResult>();
        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var result = JsonSerializer.Deserialize<CycleResult>(json, JsonDefaults.ResultJsonOptions);
            if (result is not null)
                results.Add(result);
        }

        return results;
    }

    public Task<IReadOnlyList<string>> ListCycleIdsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_resultsDirectory))
            return Task.FromResult<IReadOnlyList<string>>([]);

        var cycleIds = Directory.GetFiles(_resultsDirectory, "cycle-*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Cast<string>()
            .OrderDescending()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(cycleIds);
    }

    private string SerializeWithSanitizedErrors(CycleResult result)
    {
        // Sanitize error messages for serialization without permanently mutating the caller's object.
        // Original messages are restored in the finally block so the in-memory CycleResult remains unchanged.
        var originalMessages = new List<(EntityError error, string message)>();
        foreach (var entityResult in result.Results)
        {
            foreach (var error in entityResult.Errors)
            {
                originalMessages.Add((error, error.Message));
                error.Message = _sanitizer.Sanitize(error.Message);
            }
        }

        try
        {
            return JsonSerializer.Serialize(result, JsonDefaults.ResultJsonOptions);
        }
        finally
        {
            foreach (var (error, message) in originalMessages)
            {
                error.Message = message;
            }
        }
    }
}
