using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Mappings;

namespace Borc.DataMapper.Application.Imports.Common;

public sealed record MatchField(
    long Id,
    string Key,
    string Label,
    IReadOnlyList<string> NormalizedAliases);

public sealed record ProfileRuleHint(
    string NormalizedSource,
    string FieldKey,
    MappingMethod Method,
    decimal? Confidence);

public sealed record ColumnSuggestion(
    string Header,
    string? FieldKey,
    MappingMethod Method,
    decimal Confidence);

/// <summary>
/// پیشنهاد نگاشت ستون‌های Excel به فیلدها.
/// اولویت: قاعدهٔ پروفایل، alias، برابری با کلید یا برچسب فیلد، شباهت (≥ 0.8).
/// هر فیلد فقط به یک ستون و هر ستون فقط به یک فیلد نگاشت می‌شود.
/// </summary>
public static class ColumnMatcher
{
    private const double FuzzyThreshold = 0.8;

    public static IReadOnlyList<ColumnSuggestion> Suggest(
        IReadOnlyList<string> headers,
        IReadOnlyList<MatchField> fields,
        IReadOnlyList<ProfileRuleHint> rules)
    {
        var fieldKeys = fields.Select(f => f.Key).ToHashSet();
        var normalizedFields = fields
            .Select(f => new
            {
                Field = f,
                Key = TextNormalizer.NormalizeHeader(f.Key),
                Label = TextNormalizer.NormalizeHeader(f.Label)
            })
            .ToList();

        var candidates = new List<(int Index, string Key, MappingMethod Method, decimal Confidence)>();

        for (var i = 0; i < headers.Count; i++)
        {
            var norm = TextNormalizer.NormalizeHeader(headers[i]);

            if (norm.Length == 0)
                continue;

            foreach (var rule in rules.Where(r => r.NormalizedSource == norm && fieldKeys.Contains(r.FieldKey)))
                candidates.Add((i, rule.FieldKey, rule.Method, rule.Confidence ?? 100m));

            foreach (var nf in normalizedFields)
            {
                if (nf.Field.NormalizedAliases.Contains(norm))
                {
                    candidates.Add((i, nf.Field.Key, MappingMethod.Alias, 100m));
                }
                else if (nf.Key == norm || nf.Label == norm)
                {
                    candidates.Add((i, nf.Field.Key, MappingMethod.Auto, 95m));
                }
                else
                {
                    var best = Similarity(norm, nf.Key);
                    best = Math.Max(best, Similarity(norm, nf.Label));

                    foreach (var alias in nf.Field.NormalizedAliases)
                        best = Math.Max(best, Similarity(norm, alias));

                    if (best >= FuzzyThreshold)
                        candidates.Add((i, nf.Field.Key, MappingMethod.Auto, Math.Round((decimal)best * 90m, 2)));
                }
            }
        }

        var usedHeaders = new HashSet<int>();
        var usedFields = new HashSet<string>();
        var assigned = new Dictionary<int, (string Key, MappingMethod Method, decimal Confidence)>();

        foreach (var c in candidates.OrderByDescending(c => c.Confidence).ThenBy(c => c.Index))
        {
            if (usedHeaders.Contains(c.Index) || usedFields.Contains(c.Key))
                continue;

            usedHeaders.Add(c.Index);
            usedFields.Add(c.Key);
            assigned[c.Index] = (c.Key, c.Method, c.Confidence);
        }

        return headers
            .Select((h, i) => assigned.TryGetValue(i, out var a)
                ? new ColumnSuggestion(h, a.Key, a.Method, a.Confidence)
                : new ColumnSuggestion(h, null, MappingMethod.Manual, 0m))
            .ToList();
    }

    /// <summary>شباهت 0..1 بر اساس فاصلهٔ ویرایشی (Levenshtein).</summary>
    private static double Similarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
            return 0;

        if (a == b)
            return 1;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return 1.0 - (double)previous[b.Length] / Math.Max(a.Length, b.Length);
    }
}