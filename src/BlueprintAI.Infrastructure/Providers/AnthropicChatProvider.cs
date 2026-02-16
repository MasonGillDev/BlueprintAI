using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BlueprintAI.Application.Interfaces;

namespace BlueprintAI.Infrastructure.Providers;

public class AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-20250514";
    public int MaxTokens { get; set; } = 4096;
}

public class AnthropicChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private AnthropicSettings _settings;

    public string ProviderId => "anthropic";
    public bool SupportsNativeToolCalling => true;

    public AnthropicChatProvider(HttpClient httpClient, AnthropicSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public void UpdateSettings(AnthropicSettings settings) => _settings = settings;

    public async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(
        List<ChatMessage> messages,
        List<ToolDefinition> tools,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var anthropicMessages = ConvertMessages(messages);
        var anthropicTools = ConvertTools(tools);

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _settings.Model,
            ["max_tokens"] = _settings.MaxTokens,
            ["system"] = systemPrompt,
            ["messages"] = anthropicMessages,
            ["stream"] = true
        };

        if (anthropicTools.Count > 0)
            requestBody["tools"] = anthropicTools;

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? currentToolId = null;
        string? currentToolName = null;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "content_block_start":
                    var block = root.GetProperty("content_block");
                    if (block.GetProperty("type").GetString() == "tool_use")
                    {
                        currentToolId = block.GetProperty("id").GetString();
                        currentToolName = block.GetProperty("name").GetString();
                        yield return new CompletionChunk
                        {
                            ToolCall = new ToolCallInfo
                            {
                                Id = currentToolId!,
                                Name = currentToolName!,
                                ArgumentsJson = ""
                            }
                        };
                    }
                    break;

                case "content_block_delta":
                    var delta = root.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();

                    if (deltaType == "text_delta")
                    {
                        yield return new CompletionChunk
                        {
                            TextDelta = delta.GetProperty("text").GetString()
                        };
                    }
                    else if (deltaType == "input_json_delta" && currentToolId != null)
                    {
                        yield return new CompletionChunk
                        {
                            ToolCall = new ToolCallInfo
                            {
                                Id = currentToolId,
                                Name = currentToolName!,
                                ArgumentsJson = delta.GetProperty("partial_json").GetString() ?? ""
                            }
                        };
                    }
                    break;

                case "content_block_stop":
                    if (currentToolId != null)
                    {
                        yield return new CompletionChunk
                        {
                            ToolCall = new ToolCallInfo
                            {
                                Id = currentToolId,
                                Name = currentToolName!,
                                IsComplete = true,
                                ArgumentsJson = ""
                            }
                        };
                        currentToolId = null;
                        currentToolName = null;
                    }
                    break;

                case "message_stop":
                    yield return new CompletionChunk { IsComplete = true, StopReason = "end_turn" };
                    break;
            }
        }
    }

    private static List<object> ConvertMessages(List<ChatMessage> messages)
    {
        var result = new List<object>();

        foreach (var msg in messages)
        {
            if (msg.Role == "user")
            {
                result.Add(new { role = "user", content = msg.Content ?? "" });
            }
            else if (msg.Role == "assistant")
            {
                var content = new List<object>();
                if (!string.IsNullOrEmpty(msg.Content))
                    content.Add(new { type = "text", text = msg.Content });

                if (msg.ToolCalls != null)
                {
                    foreach (var tc in msg.ToolCalls)
                    {
                        content.Add(new
                        {
                            type = "tool_use",
                            id = tc.Id,
                            name = tc.Name,
                            input = JsonSerializer.Deserialize<JsonElement>(tc.ArgumentsJson)
                        });
                    }
                }

                result.Add(new { role = "assistant", content });
            }
            else if (msg.Role == "tool")
            {
                result.Add(new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "tool_result",
                            tool_use_id = msg.ToolCallId,
                            content = msg.Content ?? ""
                        }
                    }
                });
            }
        }

        return result;
    }

    private static List<object> ConvertTools(List<ToolDefinition> tools)
    {
        return tools.Select(t => (object)new
        {
            name = t.Name,
            description = t.Description,
            input_schema = JsonSerializer.Deserialize<JsonElement>(t.Schema.RootElement.GetRawText())
        }).ToList();
    }
}
