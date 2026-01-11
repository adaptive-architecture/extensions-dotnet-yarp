#nullable enable

using System.Text.Json;
using AdaptArch.Extensions.Yarp.OpenApi.Configuration;
using AdaptArch.Extensions.Yarp.OpenApi.Json;
using Xunit;

namespace AdaptArch.Extensions.Yarp.OpenApi.UnitTests.Json;

public class OpenApiJsonContextTests
{
    [Fact]
    public void ServiceListResponse_CanSerializeAndDeserialize()
    {
        var response = new ServiceListResponse
        {
            Services = [
                new ServiceInfo { Name = "Test Service", Url = "/api-docs/test-service" },
                new ServiceInfo { Name = "Another Service", Url = "/api-docs/another-service" }
            ],
            Count = 2
        };

        var json = JsonSerializer.Serialize(response, OpenApiJsonContext.Default.ServiceListResponse);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.ServiceListResponse);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal(2, deserialized.Services.Count);
        Assert.Equal("Test Service", deserialized.Services[0].Name);
        Assert.Equal("/api-docs/test-service", deserialized.Services[0].Url);
    }

    [Fact]
    public void ServiceListResponse_SerializesWithCamelCase()
    {
        var response = new ServiceListResponse
        {
            Services = [new ServiceInfo { Name = "Test", Url = "/test" }],
            Count = 1
        };

        var json = JsonSerializer.Serialize(response, OpenApiJsonContext.Default.ServiceListResponse);

        Assert.Contains("\"services\":", json);
        Assert.Contains("\"count\":", json);
        Assert.DoesNotContain("\"Services\":", json);
        Assert.DoesNotContain("\"Count\":", json);
    }

    [Fact]
    public void ServiceInfo_CanSerializeAndDeserialize()
    {
        var serviceInfo = new ServiceInfo
        {
            Name = "User Service",
            Url = "/api-docs/user-service"
        };

        var json = JsonSerializer.Serialize(serviceInfo, OpenApiJsonContext.Default.ServiceInfo);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.ServiceInfo);

        Assert.NotNull(deserialized);
        Assert.Equal("User Service", deserialized.Name);
        Assert.Equal("/api-docs/user-service", deserialized.Url);
    }

    [Fact]
    public void ServiceInfo_SerializesWithCamelCase()
    {
        var serviceInfo = new ServiceInfo
        {
            Name = "Test Service",
            Url = "/api-docs/test"
        };

        var json = JsonSerializer.Serialize(serviceInfo, OpenApiJsonContext.Default.ServiceInfo);

        Assert.Contains("\"name\":", json);
        Assert.Contains("\"url\":", json);
        Assert.DoesNotContain("\"Name\":", json);
        Assert.DoesNotContain("\"Url\":", json);
    }

    [Fact]
    public void ServiceListResponse_DefaultConstructor_InitializesEmptyList()
    {
        var response = new ServiceListResponse();

        Assert.NotNull(response.Services);
        Assert.Empty(response.Services);
        Assert.Equal(0, response.Count);
    }

    [Fact]
    public void ServiceInfo_DefaultConstructor_InitializesEmptyString()
    {
        var serviceInfo = new ServiceInfo();

        Assert.Equal(String.Empty, serviceInfo.Name);
        Assert.Equal(String.Empty, serviceInfo.Url);
    }

    [Fact]
    public void AdaOpenApiClusterConfig_CanSerializeWithContext()
    {
        var config = new AdaOpenApiClusterConfig
        {
            OpenApiPath = "/openapi.json",
            Prefix = "TestService"
        };

        var json = JsonSerializer.Serialize(config, OpenApiJsonContext.Default.AdaOpenApiClusterConfig);

        Assert.Contains("\"openApiPath\":", json);
        Assert.Contains("\"/openapi.json\"", json);
        Assert.Contains("\"prefix\":", json);
        Assert.Contains("\"TestService\"", json);
    }

    [Fact]
    public void AdaOpenApiRouteConfig_CanSerializeWithContext()
    {
        var config = new AdaOpenApiRouteConfig
        {
            ServiceName = "Test Service",
            Enabled = true
        };

        var json = JsonSerializer.Serialize(config, OpenApiJsonContext.Default.AdaOpenApiRouteConfig);

        Assert.Contains("\"serviceName\":", json);
        Assert.Contains("\"Test Service\"", json);
        Assert.Contains("\"enabled\":", json);
        Assert.Contains("true", json);
    }

    [Fact]
    public void ListOfServiceInfo_CanSerializeAndDeserialize()
    {
        var list = new List<ServiceInfo>
        {
            new() { Name = "Service1", Url = "/service1" },
            new() { Name = "Service2", Url = "/service2" }
        };

        var json = JsonSerializer.Serialize(list, OpenApiJsonContext.Default.ListServiceInfo);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.ListServiceInfo);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("Service1", deserialized[0].Name);
        Assert.Equal("Service2", deserialized[1].Name);
    }

    [Fact]
    public void DictionaryStringObject_CanSerializeWithContext()
    {
        var dict = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = true
        };

        var json = JsonSerializer.Serialize(dict, OpenApiJsonContext.Default.DictionaryStringObject);

        Assert.Contains("\"key1\":", json);
        Assert.Contains("\"value1\"", json);
        Assert.Contains("\"key2\":", json);
        Assert.Contains("42", json);
    }

    [Fact]
    public void DictionaryStringString_CanSerializeAndDeserialize()
    {
        var dict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        var json = JsonSerializer.Serialize(dict, OpenApiJsonContext.Default.DictionaryStringString);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.DictionaryStringString);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("value1", deserialized["key1"]);
        Assert.Equal("value2", deserialized["key2"]);
    }

    [Fact]
    public void ListOfString_CanSerializeAndDeserialize()
    {
        var list = new List<string> { "item1", "item2", "item3" };

        var json = JsonSerializer.Serialize(list, OpenApiJsonContext.Default.ListString);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.ListString);

        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Count);
        Assert.Equal("item1", deserialized[0]);
        Assert.Equal("item2", deserialized[1]);
        Assert.Equal("item3", deserialized[2]);
    }

    [Fact]
    public void String_CanSerializeAndDeserialize()
    {
        var value = "test string";

        var json = JsonSerializer.Serialize(value, OpenApiJsonContext.Default.String);
        var deserialized = JsonSerializer.Deserialize(json, OpenApiJsonContext.Default.String);

        Assert.Equal("test string", deserialized);
    }

    [Fact]
    public void ServiceListResponse_WithNullProperties_OmitsInSerialization()
    {
        var response = new ServiceListResponse
        {
            Services = null!,
            Count = 0
        };

        var json = JsonSerializer.Serialize(response, OpenApiJsonContext.Default.ServiceListResponse);

        // With JsonIgnoreCondition.WhenWritingNull, null should be omitted
        Assert.DoesNotContain("null", json.ToLowerInvariant());
    }

    [Fact]
    public void ServiceInfo_PropertiesAreSettable()
    {
        var serviceInfo = new ServiceInfo();

        serviceInfo.Name = "Updated Name";
        serviceInfo.Url = "Updated URL";

        Assert.Equal("Updated Name", serviceInfo.Name);
        Assert.Equal("Updated URL", serviceInfo.Url);
    }

    [Fact]
    public void ServiceListResponse_PropertiesAreSettable()
    {
        var response = new ServiceListResponse();

        response.Services = [new ServiceInfo { Name = "Test", Url = "/test" }];
        response.Count = 1;

        Assert.Single(response.Services);
        Assert.Equal(1, response.Count);
    }
}
