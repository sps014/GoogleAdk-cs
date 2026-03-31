// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GoogleAdk.Core.Abstractions.Tools;

namespace GoogleAdk.Samples.Orchestration;

/// <summary>
/// Tool methods decorated with [FunctionTool]. The source generator
/// auto-creates FunctionTool instances — no manual schema boilerplate needed.
/// Call <c>SampleTools.GetGeneratedTools()</c> to get them.
/// </summary>
public static partial class SampleTools
{
    /// <summary>Gets the current weather for a city. Returns temperature and conditions.</summary>
    /// <param name="city">The city name</param>
    [FunctionTool]
    public static object? GetWeather(string city)
    {
        return new Dictionary<string, object?>
        {
            ["city"] = city,
            ["temperature_celsius"] = city.Contains("New York", StringComparison.OrdinalIgnoreCase) ? 22 : 18,
            ["condition"] = city.Contains("London", StringComparison.OrdinalIgnoreCase) ? "Rainy" : "Sunny",
            ["humidity_percent"] = 65,
            ["wind_kph"] = 12,
        };
    }

    /// <summary>Searches for recent news headlines about a topic.</summary>
    /// <param name="topic">The topic to search news for</param>
    [FunctionTool]
    public static object? SearchNews(string topic)
    {
        var headlines = new[]
        {
            $"Breaking: New developments in {topic} sector show promising growth",
            $"Analysis: How {topic} is reshaping the global economy in 2025",
            $"Expert opinion: The future of {topic} according to industry leaders",
        };
        return new { topic, headlines, source = "simulated-news-api" };
    }

    /// <summary>Performs basic math calculations. Supports +, -, *, /.</summary>
    /// <param name="expression">A math expression like '2 + 3 * 4'</param>
    [FunctionTool]
    public static object? Calculate(string expression)
    {
        try
        {
            var result = new System.Data.DataTable().Compute(expression, null);
            return new { expression, result = result?.ToString() };
        }
        catch
        {
            return new { expression, error = "Invalid expression" };
        }
    }
}
