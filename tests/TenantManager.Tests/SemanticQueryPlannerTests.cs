using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TenantManager.App.Data;
using TenantManager.Core.Services.AI;
using Xunit;

namespace TenantManager.Tests;

public class SemanticQueryPlannerTests
{
    private class MockPlannerHttpMessageHandler : HttpMessageHandler
    {
        private readonly string? _responseContent;
        private readonly HttpStatusCode _statusCode;
        public string? LastRequestBody { get; private set; }

        public MockPlannerHttpMessageHandler(string? responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(_statusCode);
            if (_responseContent != null)
            {
                response.Content = new StringContent(_responseContent);
            }
            return response;
        }
    }

    [Fact]
    public async Task PlanQueryAsync_ValidJsonResponse_ParsedCorrectly()
    {
        // Arrange
        var validResponse = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""{\""language\"": \""es\"", \""resource\"": \""payments\"", \""operation\"": \""count\"", \""filters\"": [{\""field\"": \""late\"", \""operator\"": \""equals\"", \""value\"": true}], \""confidence\"": 0.95}""
                    }
                }
            ]
        }";

        var mockHandler = new MockPlannerHttpMessageHandler(validResponse);
        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var planner = new SemanticQueryPlanner(aiClient);

        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        // Act
        var plan = await planner.PlanQueryAsync("¿Hay pagos atrasados?");

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("es", plan.Language);
        Assert.Equal(SemanticQueryResource.Payments, plan.Resource);
        Assert.Equal(SemanticQueryOperation.Count, plan.Operation);
        Assert.Single(plan.Filters);
        Assert.Equal("late", plan.Filters[0].Field);
        Assert.Equal(SemanticQueryOperator.Equals, plan.Filters[0].Operator);
        Assert.True(((JsonElement)plan.Filters[0].Value!).GetBoolean());
        Assert.Equal(0.95, plan.Confidence);
    }

    [Fact]
    public async Task PlanQueryAsync_MarkdownWrappedJson_ParsedCorrectly()
    {
        // Arrange
        var markdownResponse = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""```json\n{\""language\"": \""es\"", \""resource\"": \""payments\"", \""operation\"": \""count\"", \""confidence\"": 0.9}\n```""
                    }
                }
            ]
        }";

        var mockHandler = new MockPlannerHttpMessageHandler(markdownResponse);
        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var planner = new SemanticQueryPlanner(aiClient);

        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        // Act
        var plan = await planner.PlanQueryAsync("¿Hay pagos atrasados?");

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("es", plan.Language);
        Assert.Equal(SemanticQueryResource.Payments, plan.Resource);
        Assert.Equal(SemanticQueryOperation.Count, plan.Operation);
        Assert.Equal(0.9, plan.Confidence);
    }

    [Fact]
    public async Task PlanQueryAsync_InvalidJson_ReturnsNullGracefully()
    {
        // Arrange
        var malformedResponse = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""{\""language\"": \""es\"", \""resource\"": \""payments\"" malformed json...}""
                    }
                }
            ]
        }";

        var mockHandler = new MockPlannerHttpMessageHandler(malformedResponse);
        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var planner = new SemanticQueryPlanner(aiClient);

        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        // Act
        var plan = await planner.PlanQueryAsync("¿Hay pagos atrasados?");

        // Assert
        Assert.Null(plan);
    }

    [Fact]
    public async Task PlanQueryAsync_EndpointOffline_ReturnsNullGracefully()
    {
        // Arrange
        var mockHandler = new MockPlannerHttpMessageHandler(null, HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var planner = new SemanticQueryPlanner(aiClient);

        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        // Act
        var plan = await planner.PlanQueryAsync("¿Hay pagos atrasados?");

        // Assert
        Assert.Null(plan);
    }

    [Fact]
    public async Task PlanQueryAsync_ConversationContextInjectedIntoPrompt()
    {
        // Arrange
        var validResponse = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""{\""language\"": \""es\"", \""resource\"": \""tenants\"", \""operation\"": \""lookup\""}""
                    }
                }
            ]
        }";

        var mockHandler = new MockPlannerHttpMessageHandler(validResponse);
        var httpClient = new HttpClient(mockHandler);
        var aiClient = new LocalAiClient(httpClient);
        var planner = new SemanticQueryPlanner(aiClient);

        SettingsPersistence.SaveSettings(new AppSettings { IsAiEnabled = true, AiEndpoint = "http://mock" });

        var context = new AssistantContext
        {
            LastResolvedIntent = "tenant_move_out_date",
            LastLanguage = "es",
            LastEntityType = "tenantName"
        };

        // Act
        await planner.PlanQueryAsync("¿Y Namratha?", context);

        // Assert
        var requestBody = mockHandler.LastRequestBody;
        Assert.NotNull(requestBody);
        Assert.Contains("previous_intent=tenant_move_out_date", requestBody);
        Assert.Contains("previous_language=es", requestBody);
    }
}
