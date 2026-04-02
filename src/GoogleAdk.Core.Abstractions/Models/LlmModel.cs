namespace GoogleAdk.Core.Abstractions.Models;

/// <summary>
/// Represents a model reference that can be either a <see cref="BaseLlm"/> instance or a model name string.
/// Use implicit conversion from <see cref="string"/> or <see cref="BaseLlm"/>.
/// </summary>
public sealed class LlmModel
{
    internal BaseLlm? Instance { get; }
    internal string? Name { get; }

    private LlmModel(BaseLlm instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    private LlmModel(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(name));
        Name = name;
    }

    public static implicit operator LlmModel(string modelName) => new(modelName);
    public static implicit operator LlmModel(BaseLlm model) => new(model);

    /// <summary>
    /// Resolves to a <see cref="BaseLlm"/> instance, using <see cref="LlmRegistry"/> for name-based references.
    /// </summary>
    public BaseLlm Resolve()
    {
        if (Instance != null) return Instance;
        return LlmRegistry.NewLlm(Name!);
    }

    /// <summary>
    /// Gets the model name string (from the instance or the stored name).
    /// </summary>
    public string ModelName => Instance?.Model ?? Name!;
}
