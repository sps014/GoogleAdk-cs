using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace GoogleAdk.Core.Agents;

internal static class SchemaHelper
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Default;

    /// <summary>
    /// Converts a CLR type to a JSON Schema dictionary using System.Text.Json,
    /// then normalises the output for the Gemini API (which requires scalar
    /// <c>"type"</c> strings, not arrays like <c>["string","null"]</c>).
    /// </summary>
    public static Dictionary<string, object?> TypeToSchemaDict(Type type)
    {
        var schemaNode = Options.GetJsonSchemaAsNode(type);
        NormaliseNode(schemaNode);
        return schemaNode.Deserialize<Dictionary<string, object?>>()
            ?? throw new InvalidOperationException($"Failed to generate JSON schema for type {type.FullName}.");
    }

    /// <summary>
    /// Recursively rewrites <c>"type": ["foo", "null"]</c> → <c>"type": "foo", "nullable": true</c>
    /// so the schema is compatible with the Gemini API's structured output format.
    /// Also ensures every <c>"type": "object"</c> node has a <c>"properties"</c> key.
    /// </summary>
    private static void NormaliseNode(JsonNode? node)
    {
        if (node is not JsonObject obj) return;

        // Normalise "type" arrays → scalar + nullable
        if (obj.TryGetPropertyValue("type", out var typeNode) && typeNode is JsonArray typeArray)
        {
            var types = typeArray.Select(t => t?.GetValue<string>()).Where(t => t != null).ToList();
            bool isNullable = types.Remove("null");
            if (types.Count == 1)
            {
                obj["type"] = JsonValue.Create(types[0]);
                if (isNullable)
                    obj["nullable"] = JsonValue.Create(true);
            }
        }

        // Ensure "type": "object" nodes have "properties"
        if (obj.TryGetPropertyValue("type", out var scalarType)
            && scalarType is JsonValue v
            && v.ToString() == "object"
            && !obj.ContainsKey("properties"))
        {
            obj["properties"] = new JsonObject();
        }

        // Recurse into properties
        if (obj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject props)
        {
            foreach (var kvp in props)
                NormaliseNode(kvp.Value);
        }

        // Recurse into items (array element schema)
        if (obj.TryGetPropertyValue("items", out var itemsNode))
            NormaliseNode(itemsNode);

        // Recurse into anyOf/oneOf/allOf
        foreach (var keyword in new[] { "anyOf", "oneOf", "allOf" })
        {
            if (obj.TryGetPropertyValue(keyword, out var compositeNode) && compositeNode is JsonArray arr)
            {
                foreach (var element in arr)
                    NormaliseNode(element);
            }
        }
    }
}
