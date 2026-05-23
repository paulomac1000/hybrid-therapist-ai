using System.Text.Json;
using HybridTherapist.Application.Flows;
using HybridTherapist.Domain.Models;

namespace HybridTherapist.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/chat/completions", HandleCompletions)
            .WithName("ChatCompletions");

        app.MapGet("/v1/models", HandleModels)
            .WithName("ListModels");
    }

    private static async Task HandleCompletions(
        HttpContext ctx,
        TherapistFlow flow,
        CancellationToken ct)
    {
        ChatCompletionRequest? request;
        try
        {
            request = await ctx.Request.ReadFromJsonAsync<ChatCompletionRequest>(ct);
        }
        catch
        {
            await WriteJsonAsync(ctx, 400, new { error = "Invalid JSON body." }, ct);
            return;
        }

        if (request is null)
        {
            await WriteJsonAsync(ctx, 400, new { error = "Request body is required." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            await WriteJsonAsync(ctx, 400, new { error = "Field 'model' is required." }, ct);
            return;
        }

        if (request.Messages is null || request.Messages.Count == 0)
        {
            await WriteJsonAsync(ctx, 400, new { error = "Field 'messages' must be non-empty." }, ct);
            return;
        }

        FlowExecutionResult result = await flow.ExecuteAsync(request, ct);

        ctx.Response.Headers["X-Cortexa-Flow"] = "hybrid-therapist";
        ctx.Response.Headers["X-Cortexa-Fallback"] = result.Fallback ? "true" : "false";

        if (request.Stream)
        {
            await WriteSseAsync(ctx, result, ct);
            return;
        }

        var response = new
        {
            id = $"chatcmpl-{Guid.NewGuid():N}",
            @object = "chat.completion",
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            model = result.Model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = result.Content },
                    finish_reason = "stop",
                },
            },
            usage = new { prompt_tokens = 0, completion_tokens = 0, total_tokens = 0 },
            metadata = result.Metadata,
        };

        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(response, ct);
    }

    /// <summary>
    /// Emits OpenAI-compatible SSE chunks. LibreChat (and most OpenAI clients) sends
    /// <c>stream: true</c> by default and parses <c>delta.role</c> + <c>delta.content</c>
    /// from each chunk; returning a single JSON body confuses the client.
    /// We don't actually stream — the pipeline is batch — so we emit one content chunk
    /// followed by a final <c>finish_reason: stop</c> chunk and the <c>[DONE]</c> sentinel.
    /// </summary>
    private static async Task WriteSseAsync(HttpContext ctx, FlowExecutionResult result, CancellationToken ct)
    {
        ctx.Response.StatusCode = StatusCodes.Status200OK;
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";

        string id = $"chatcmpl-{Guid.NewGuid():N}";
        long created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var firstChunk = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["object"] = "chat.completion.chunk",
            ["created"] = created,
            ["model"] = result.Model,
            ["choices"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["index"] = 0,
                    ["delta"] = new Dictionary<string, object?>
                    {
                        ["role"] = "assistant",
                        ["content"] = result.Content,
                    },
                    ["finish_reason"] = null,
                },
            },
        };
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(firstChunk)}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        var finalChunk = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["object"] = "chat.completion.chunk",
            ["created"] = created,
            ["model"] = result.Model,
            ["choices"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["index"] = 0,
                    ["delta"] = new Dictionary<string, object?>(),
                    ["finish_reason"] = "stop",
                },
            },
        };
        await ctx.Response.WriteAsync($"data: {JsonSerializer.Serialize(finalChunk)}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    private static async Task WriteJsonAsync(HttpContext ctx, int status, object body, CancellationToken ct)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(body, ct);
    }

    private static IResult HandleModels()
    {
        var response = new
        {
            @object = "list",
            data = new[]
            {
                new
                {
                    id = "hybrid-therapist",
                    @object = "model",
                    created = 1_700_000_000L,
                    owned_by = "hybrid-therapist",
                },
            },
        };
        return Results.Ok(response);
    }
}
