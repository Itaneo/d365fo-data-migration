namespace Dynamics365ImportData.Comparison;

using Dynamics365ImportData.Comparison.Models;
using Dynamics365ImportData.Persistence;
using Dynamics365ImportData.Persistence.Models;
using Dynamics365ImportData.Pipeline;

using Microsoft.Extensions.Logging;

public class ErrorComparisonService : IErrorComparisonService
{
    private readonly IMigrationResultRepository _repository;
    private readonly ILogger<ErrorComparisonService> _logger;

    public ErrorComparisonService(
        IMigrationResultRepository repository,
        ILogger<ErrorComparisonService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ComparisonResult> CompareAsync(
        string? currentCycleId = null,
        string? previousCycleId = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;

        CycleResult? current;
        CycleResult? previous;

        if (currentCycleId is null)
        {
            var latest = await _repository.GetLatestCycleResultsAsync(2, cancellationToken);
            current = latest.Count > 0 ? latest[0] : null;
            previous = latest.Count > 1 ? latest[1] : null;
        }
        else
        {
            current = await _repository.GetCycleResultAsync(currentCycleId, cancellationToken);
            previous = previousCycleId is not null
                ? await _repository.GetCycleResultAsync(previousCycleId, cancellationToken)
                : (await _repository.GetLatestCycleResultsAsync(2, cancellationToken))
                    .FirstOrDefault(c => c.CycleId != currentCycleId);
        }

        if (current is null)
        {
            _logger.LogInformation("No cycle results found -- nothing to compare");
            return new ComparisonResult { IsFirstCycle = true, Timestamp = timestamp };
        }

        if (previous is null)
        {
            _logger.LogInformation("First cycle -- no comparison available");
            return new ComparisonResult
            {
                CurrentCycleId = current.CycleId,
                IsFirstCycle = true,
                Timestamp = timestamp
            };
        }

        return BuildComparison(current, previous, timestamp, cancellationToken);
    }

    private static ComparisonResult BuildComparison(
        CycleResult current,
        CycleResult previous,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var previousErrorsByEntity = previous.Results
            .GroupBy(r => r.EntityName)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(r => r.Errors)
                      .Select(e => e.Fingerprint)
                      .Where(f => !string.IsNullOrEmpty(f))
                      .ToHashSet());

        var currentEntityNames = current.Results.Select(r => r.EntityName).ToHashSet();

        var entityComparisons = new List<EntityComparisonResult>();
        var totalNew = 0;
        var totalCarryOver = 0;
        var totalResolved = 0;

        // Process entities in current cycle
        foreach (var entityResult in current.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entityName = entityResult.EntityName;
            var previousFingerprints = previousErrorsByEntity.GetValueOrDefault(entityName) ?? new HashSet<string>();

            var hasCurrentErrors = entityResult.Errors.Count > 0;
            var hasPreviousErrors = previousFingerprints.Count > 0;

            if (!hasCurrentErrors && !hasPreviousErrors)
                continue;

            var comparison = new EntityComparisonResult
            {
                EntityName = entityName,
                CurrentStatus = entityResult.Status
            };

            var currentFingerprints = new HashSet<string>();

            foreach (var error in entityResult.Errors)
            {
                if (!string.IsNullOrEmpty(error.Fingerprint))
                    currentFingerprints.Add(error.Fingerprint);

                var classification = previousFingerprints.Contains(error.Fingerprint)
                    ? ErrorClassification.CarryOver
                    : ErrorClassification.New;

                var classifiedError = new ClassifiedError
                {
                    EntityName = entityName,
                    Message = error.Message,
                    Fingerprint = error.Fingerprint,
                    Classification = classification,
                    Category = error.Category
                };

                if (classification == ErrorClassification.New)
                    comparison.NewErrors.Add(classifiedError);
                else
                    comparison.CarryOverErrors.Add(classifiedError);
            }

            // Find resolved: fingerprints in previous but not in current
            foreach (var fingerprint in previousFingerprints)
            {
                if (!currentFingerprints.Contains(fingerprint))
                    comparison.ResolvedFingerprints.Add(fingerprint);
            }

            totalNew += comparison.NewErrors.Count;
            totalCarryOver += comparison.CarryOverErrors.Count;
            totalResolved += comparison.ResolvedFingerprints.Count;

            entityComparisons.Add(comparison);
        }

        // Process entities that exist in previous but not in current -- all resolved
        foreach (var previousResult in previous.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (currentEntityNames.Contains(previousResult.EntityName))
                continue;

            var previousFingerprints = previousResult.Errors
                .Select(e => e.Fingerprint)
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            if (previousFingerprints.Count == 0)
                continue;

            var comparison = new EntityComparisonResult
            {
                EntityName = previousResult.EntityName,
                CurrentStatus = EntityStatus.Success,
                ResolvedFingerprints = previousFingerprints
            };

            totalResolved += previousFingerprints.Count;
            entityComparisons.Add(comparison);
        }

        return new ComparisonResult
        {
            CurrentCycleId = current.CycleId,
            PreviousCycleId = previous.CycleId,
            Timestamp = timestamp,
            IsFirstCycle = false,
            EntityComparisons = entityComparisons,
            TotalNewErrors = totalNew,
            TotalCarryOverErrors = totalCarryOver,
            TotalResolvedErrors = totalResolved
        };
    }
}
