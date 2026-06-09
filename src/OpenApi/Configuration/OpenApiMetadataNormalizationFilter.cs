using System.Buffers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;

namespace AdaptArch.Extensions.Yarp.OpenApi.Configuration;

/// <summary>
/// YARP config filter that normalizes metadata values written as native JSON objects in configuration.
/// </summary>
/// <remarks>
/// YARP metadata is bound as <c>IReadOnlyDictionary&lt;string, string&gt;</c>. When a metadata value
/// is written as a JSON object (not a quoted string) in appsettings.json, ASP.NET Core flattens it
/// into child configuration keys and YARP omits the key from the metadata dictionary.
/// This filter detects such cases and serializes the IConfiguration sub-section back to a JSON string,
/// making both formats equivalent:
/// <code>
/// // Old format (escaped string):
/// "Ada.OpenApi": "{\"serviceName\":\"User Management\",\"enabled\":true}"
///
/// // New format (native JSON object):
/// "Ada.OpenApi": { "serviceName": "User Management", "enabled": true }
/// </code>
/// Keys that already have a non-null string value are left untouched, ensuring full backward compatibility.
/// </remarks>
public sealed class OpenApiMetadataNormalizationFilter : IProxyConfigFilter
{
    private readonly IConfiguration _configuration;
    private readonly string _sectionName;

    /// <summary>
    /// Initializes a new instance of <see cref="OpenApiMetadataNormalizationFilter"/>.
    /// </summary>
    public OpenApiMetadataNormalizationFilter(IConfiguration configuration, IOptions<OpenApiAggregationOptions> options)
    {
        _configuration = configuration;
        _sectionName = options.Value.ReverseProxyConfigSectionName;
    }

    /// <inheritdoc/>
    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        var metadataSection = _configuration.GetSection($"{_sectionName}:Routes:{route.RouteId}:Metadata");
        var updated = NormalizeMetadata(route.Metadata, metadataSection);
        return updated is null
            ? ValueTask.FromResult(route)
            : ValueTask.FromResult(route with { Metadata = updated });
    }

    /// <inheritdoc/>
    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        var metadataSection = _configuration.GetSection($"{_sectionName}:Clusters:{cluster.ClusterId}:Metadata");
        var updated = NormalizeMetadata(cluster.Metadata, metadataSection);
        return updated is null
            ? ValueTask.FromResult(cluster)
            : ValueTask.FromResult(cluster with { Metadata = updated });
    }

    private static Dictionary<string, string>? NormalizeMetadata(
        IReadOnlyDictionary<string, string>? existing,
        IConfigurationSection metadataSection)
    {
        if (!metadataSection.Exists())
        {
            return null;
        }

        Dictionary<string, string>? result = null;

        foreach (var keySection in metadataSection.GetChildren())
        {
            // Only process keys whose value is a nested object (has children)
            var children = keySection.GetChildren().ToList();
            if (children.Count == 0)
            {
                continue;
            }

            // Skip if the key already has a non-null string value — old format wins
            if (existing?.GetValueOrDefault(keySection.Key) is not null)
            {
                continue;
            }

            result ??= existing is not null
                ? new Dictionary<string, string>(existing)
                : [];

            result[keySection.Key] = BuildJsonNode(keySection)?.ToJsonString() ?? "null";
        }

        return result;
    }

    private static JsonNode? BuildJsonNode(IConfigurationSection section)
    {
        var children = section.GetChildren().ToList();
        if (children.Count > 0)
        {
            var obj = new JsonObject();
            foreach (var child in children)
            {
                obj[child.Key] = BuildJsonNode(child);
            }

            return obj;
        }

        var value = section.Value;
        if (value is null)
        {
            return null; // becomes JSON null in the parent object
        }

        if (Boolean.TryParse(value, out var boolVal))
        {
            return JsonValue.Create(boolVal);
        }

        if (Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longVal))
        {
            return JsonValue.Create(longVal);
        }

        // Write the string value via Utf8JsonWriter and parse back — avoids the generic
        // JsonValue.Create<T> overload that requires unreferenced code for AOT compatibility.
        var buffer = new ArrayBufferWriter<byte>(value.Length + 2);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        return JsonNode.Parse(buffer.WrittenSpan);
    }
}
