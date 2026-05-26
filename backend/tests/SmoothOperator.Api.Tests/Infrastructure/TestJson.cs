using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmoothOperator.Api.Tests.Infrastructure;

/// <summary>
/// JSON options matching the production API's serializer so tests can deserialize
/// string-form enums (registered globally via <c>AddJsonOptions</c>).
/// </summary>
public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
