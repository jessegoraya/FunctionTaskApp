using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Taslow.Task.Client;
using Xunit;

namespace FunctionTaskApp.Tests;

public class ProjectServiceClientTests
{
    [Theory]
    [InlineData("https://example.test/FunctionProjectApp")]
    [InlineData("https://example.test/FunctionProjectApp/")]
    public async Task GetActiveProjectsAsync_PreservesBasePath(string baseUrl)
    {
        var handler = new RecordingHandler("[]");
        var client = CreateClient(baseUrl, handler);

        await client.GetActiveProjectsAsync("tenant-1", "test-token");

        Assert.Equal(
            "https://example.test/FunctionProjectApp/projects/active/tenant-1",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetProjectIdsForManagerAsync_PreservesBasePath()
    {
        var handler = new RecordingHandler("[]");
        var client = CreateClient("https://example.test/FunctionProjectApp", handler);

        await client.GetProjectIdsForManagerAsync("tenant-1", "manager-1", "test-token");

        Assert.Equal(
            "https://example.test/FunctionProjectApp/projects/managed/tenant-1/manager-1",
            handler.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetProjectsAsync_PreservesBasePath()
    {
        var handler = new RecordingHandler("{\"projects\":[]}");
        var client = CreateClient("https://example.test/FunctionProjectApp", handler);

        await client.GetProjectsAsync(["project-1"], "tenant-1", "test-token");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://example.test/FunctionProjectApp/projects/batch",
            handler.RequestUri?.AbsoluteUri);
    }

    private static ProjectServiceClient CreateClient(string baseUrl, RecordingHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl)
        };

        return new ProjectServiceClient(
            httpClient,
            NullLogger<ProjectServiceClient>.Instance);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public Uri RequestUri { get; private set; } = null!;

        public HttpMethod Method { get; private set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}
