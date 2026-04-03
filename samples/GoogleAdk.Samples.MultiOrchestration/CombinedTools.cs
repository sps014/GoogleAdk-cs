using System.Text.RegularExpressions;
using GoogleAdk.Core.Abstractions.Tools;
using GoogleAdk.Core.Tools;

namespace GoogleAdk.Samples.Combined;

/// <summary>
/// Tools for the combined patterns sample.
/// </summary>
public static partial class CombinedTools
{
    /// <summary>
    /// Counts word frequency in the given text and returns the top N words as a markdown table.
    /// </summary>
    [FunctionTool]
    public static object? CountWordFrequency(string text, int topN = 5)
    {
        // Normalize and split into words
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length > 3) // skip short/common words
            .Where(w => !StopWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(topN)
            .Select(g => new { Word = g.Key, Count = g.Count() })
            .ToList();

        // Build markdown table
        var table = "| Rank | Word | Count |\n|------|------|-------|\n";
        for (var i = 0; i < words.Count; i++)
        {
            table += $"| {i + 1} | {words[i].Word} | {words[i].Count} |\n";
        }

        return new { markdown_table = table, total_words_analyzed = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length };
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "that", "this", "with", "from", "have", "has", "been",
        "will", "would", "could", "should", "their", "there", "they", "about",
        "which", "when", "what", "where", "also", "more", "than", "into",
        "some", "other", "over", "just", "like", "your", "said", "were",
        "each", "make", "made"
    };

    [GeneratedRegex(@"\b[a-z]+\b")]
    private static partial Regex WordRegex();
}
