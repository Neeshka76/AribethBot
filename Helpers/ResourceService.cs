using Newtonsoft.Json;

namespace AribethBot.Helpers;

public class ResourceService
{
    private readonly string resourcePath;
    private Dictionary<string, string> resources;

    public ResourceService(string path)
    {
        resourcePath = path;

        if (!File.Exists(resourcePath))
        {
            File.WriteAllText(resourcePath, "{}");
        }

        string json = File.ReadAllText(resourcePath);
        resources = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
    }

    public string? Get(string key)
    {
        return resources.TryGetValue(key, out string? value) ? value : null;
    }

    public void Set(string key, string value)
    {
        resources[key] = value;
        Save();
    }

    private void Save()
    {
        string json = JsonConvert.SerializeObject(resources, Formatting.Indented);
        File.WriteAllText(resourcePath, json);
    }
}