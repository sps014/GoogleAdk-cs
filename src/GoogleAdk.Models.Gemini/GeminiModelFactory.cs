// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using GenerativeAI;
using GenerativeAI.Microsoft;
using GoogleAdk.Core.Abstractions.Models;
using GoogleAdk.Models.Meai;

namespace GoogleAdk.Models.Gemini;

/// <summary>
/// Factory for creating Gemini-based LLM instances using Google AI or Vertex AI.
/// Reads configuration from environment variables or explicit parameters.
/// </summary>
/// <remarks>
/// Environment variables:
/// <list type="bullet">
///   <item><c>GOOGLE_GENAI_USE_VERTEXAI</c> — Set to "True" to use Vertex AI (default: false, uses AI Studio)</item>
///   <item><c>GOOGLE_CLOUD_PROJECT</c> — The GCP project ID (required for Vertex AI)</item>
///   <item><c>GOOGLE_CLOUD_LOCATION</c> — The GCP region (required for Vertex AI, e.g. "us-central1")</item>
///   <item><c>GOOGLE_API_KEY</c> — API key for Google AI Studio (required when not using Vertex AI)</item>
/// </list>
/// </remarks>
public static class GeminiModelFactory
{
    /// <summary>
    /// Creates a Gemini <see cref="BaseLlm"/> from environment variables.
    /// </summary>
    /// <param name="model">The model name, e.g. "gemini-2.5-flash" or "gemini-2.0-flash".</param>
    /// <returns>A <see cref="BaseLlm"/> backed by Gemini via the MEAI IChatClient abstraction.</returns>
    public static BaseLlm Create(string model)
    {
        var useVertexAi = string.Equals(
            Environment.GetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI"),
            "True", StringComparison.OrdinalIgnoreCase);

        if (useVertexAi)
        {
            var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                ?? throw new InvalidOperationException(
                    "GOOGLE_CLOUD_PROJECT environment variable is required when GOOGLE_GENAI_USE_VERTEXAI=True");

            var location = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_LOCATION")
                ?? "us-central1";

            return CreateVertexAi(model, projectId, location);
        }
        else
        {
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                ?? throw new InvalidOperationException(
                    "GOOGLE_API_KEY environment variable is required when not using Vertex AI");

            return CreateGoogleAi(model, apiKey);
        }
    }

    /// <summary>
    /// Creates a Gemini <see cref="BaseLlm"/> using Vertex AI with explicit parameters.
    /// Uses Application Default Credentials (ADC) for authentication.
    /// </summary>
    /// <param name="model">The model name, e.g. "gemini-2.5-flash".</param>
    /// <param name="projectId">The GCP project ID.</param>
    /// <param name="location">The GCP region (e.g. "us-central1").</param>
    /// <returns>A <see cref="BaseLlm"/> backed by Vertex AI Gemini.</returns>
    public static BaseLlm CreateVertexAi(string model, string projectId, string location)
    {
        var platform = new VertextPlatformAdapter(projectId, location);
        var chatClient = new GenerativeAIChatClient(platform, model, autoCallFunction: false);
        return new MeaiLlm(model, chatClient);
    }

    /// <summary>
    /// Creates a Gemini <see cref="BaseLlm"/> using Google AI Studio with an API key.
    /// </summary>
    /// <param name="model">The model name, e.g. "gemini-2.5-flash".</param>
    /// <param name="apiKey">The Google AI Studio API key.</param>
    /// <returns>A <see cref="BaseLlm"/> backed by Google AI Studio Gemini.</returns>
    public static BaseLlm CreateGoogleAi(string model, string apiKey)
    {
        var chatClient = new GenerativeAIChatClient(apiKey, model, autoCallFunction: false);
        return new MeaiLlm(model, chatClient);
    }
}
