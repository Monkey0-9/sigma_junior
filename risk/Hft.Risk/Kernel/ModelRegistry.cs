using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hft.Risk.Kernel;

/// <summary>
/// Registry for all approved risk models.
/// Tracks model lineage, version, and approval status.
/// </summary>
public class ModelRegistry
{
    private readonly ConcurrentDictionary<string, ModelMetadata> _registry = new();

    public void Register(string modelId, string version, string author, string description)
    {
        _registry[modelId] = new ModelMetadata(modelId, version, author, description, DateTime.UtcNow);
    }

    public ModelMetadata? GetMetadata(string modelId)
    {
        _registry.TryGetValue(modelId, out var metadata);
        return metadata;
    }
}

public record ModelMetadata(
    string Id,
    string Version,
    string Author,
    string Description,
    DateTime RegisteredAt
);
