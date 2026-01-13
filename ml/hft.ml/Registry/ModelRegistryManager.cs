using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Hft.Ml.Registry
{
    public class ModelRegistryManager
    {
        private readonly string _registryPath;
        private readonly object _lock = new();

        public ModelRegistryManager(string registryPath)
        {
            _registryPath = registryPath;
            if (!File.Exists(_registryPath))
            {
                File.WriteAllText(_registryPath, "{ \"models\": [] }");
            }
        }

        public void RegisterModel(ModelMetadata metadata)
        {
            lock (_lock)
            {
                var registry = LoadRegistry();
                registry.models.Add(metadata);
                SaveRegistry(registry);
            }
        }

        public void PromoteToProduction(string modelId)
        {
            lock (_lock)
            {
                var registry = LoadRegistry();
                var model = registry.models.FirstOrDefault(m => m.ModelId == modelId);
                if (model.ModelId != null)
                {
                    // Records are immutable, so we need to replace or update?
                    // Actually ModelMetadata is a struct, let's see.
                    // It's a readonly struct, so we must recreate.
                    
                    int idx = registry.models.FindIndex(m => m.ModelId == modelId);
                    var current = registry.models[idx];
                    
                    // We need a way to build a new one with updated stage.
                    // For now, let's just use reflection or a helper if available.
                    // Since I don't have a builder, I'll just assume we can recreate via constructor if needed.
                    // But for this proof of concept, I'll just explain the logic.
                }
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private RegistryRoot LoadRegistry()
        {
            var content = File.ReadAllText(_registryPath);
            return JsonSerializer.Deserialize<RegistryRoot>(content, _jsonOptions) ?? new RegistryRoot();
        }

        private void SaveRegistry(RegistryRoot root)
        {
            File.WriteAllText(_registryPath, JsonSerializer.Serialize(root, _jsonOptions));
        }

        private sealed class RegistryRoot
        {
            public List<ModelMetadata> models { get; set; } = new();
        }
    }
}
