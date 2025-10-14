using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ResultsService.Contracts;

internal static class RunRequestValidator
{
    public static IDictionary<string, string[]> Validate(RunRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddError(string key, string message)
        {
            if (!errors.TryGetValue(key, out var list))
            {
                list = new List<string>();
                errors[key] = list;
            }

            list.Add(message);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(nameof(request.Name), "Name is required.");
        }
        else if (request.Name.Length > 200)
        {
            AddError(nameof(request.Name), "Name must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Environment))
        {
            AddError(nameof(request.Environment), "Environment is required.");
        }
        else if (request.Environment.Length > 100)
        {
            AddError(nameof(request.Environment), "Environment must be 100 characters or fewer.");
        }

        if (request.StartedAt == default)
        {
            AddError(nameof(request.StartedAt), "StartedAt is required.");
        }

        if (request.CompletedAt.HasValue && request.CompletedAt < request.StartedAt)
        {
            AddError(nameof(request.CompletedAt), "CompletedAt cannot be earlier than StartedAt.");
        }

        if (request.Metrics is null || request.Metrics.Count == 0)
        {
            AddError(nameof(request.Metrics), "At least one metric is required.");
        }
        else
        {
            for (var i = 0; i < request.Metrics.Count; i++)
            {
                var metric = request.Metrics[i];
                var prefix = $"Metrics[{i}]";

                if (string.IsNullOrWhiteSpace(metric.Name))
                {
                    AddError($"{prefix}.{nameof(metric.Name)}", "Metric name is required.");
                }
                else if (metric.Name.Length > 100)
                {
                    AddError($"{prefix}.{nameof(metric.Name)}", "Metric name must be 100 characters or fewer.");
                }

                if (!string.IsNullOrEmpty(metric.Unit) && metric.Unit.Length > 50)
                {
                    AddError($"{prefix}.{nameof(metric.Unit)}", "Metric unit must be 50 characters or fewer.");
                }
            }
        }

        return errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }
}
