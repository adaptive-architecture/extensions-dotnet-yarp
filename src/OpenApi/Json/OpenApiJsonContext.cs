namespace AdaptArch.Extensions.Yarp.OpenApi.Json;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;

/// <summary>
/// JSON serializer context for AOT compatibility
/// </summary>
[JsonSerializable(typeof(ServiceListResponse))]
[JsonSerializable(typeof(ServiceInfo))]
[JsonSerializable(typeof(List<ServiceInfo>))]
[JsonSerializable(typeof(AdaOpenApiClusterConfig))]
[JsonSerializable(typeof(AdaOpenApiRouteConfig))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class OpenApiJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Response type for service list endpoint
/// </summary>
internal sealed class ServiceListResponse
{
    public List<ServiceInfo> Services { get; set; } = [];
    public int Count { get; set; }
}

/// <summary>
/// Information about a service in the list
/// </summary>
internal sealed class ServiceInfo
{
    public string Name { get; set; } = String.Empty;
    public string Url { get; set; } = String.Empty;
}
