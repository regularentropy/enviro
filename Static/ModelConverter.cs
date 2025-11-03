using enviro.Models;
using System.Text.Json;

namespace enviro.Static;

internal static class ModelConverter
{
    public static string SerializeJSON(EnvModelBundle bundle) =>
        JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });

    public static EnvModelBundle DeserializeJSON(string json) =>
        JsonSerializer.Deserialize<EnvModelBundle>(json)!;
}
