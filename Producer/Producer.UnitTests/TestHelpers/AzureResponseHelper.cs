using Azure;
using System.Reflection;

namespace Producer.UnitTests.TestHelpers;

/// <summary>
/// Helper class to create Azure Response objects for testing
/// </summary>
public static class AzureResponseHelper
{
    /// <summary>
    /// Creates a mock Response<T> using reflection
    /// </summary>
    public static Response<T> CreateResponse<T>(T value, int status = 200)
    {
        // This is a workaround since Response<T> is abstract and sealed
        // In practice, returning null from CreateIfNotExistsAsync is sufficient for tests
        var mockResponse = new Mock<Response<T>>();
        mockResponse.Setup(r => r.Value).Returns(value);
        mockResponse.Setup(r => r.GetRawResponse().Status).Returns(status);
        return mockResponse.Object;
    }
}