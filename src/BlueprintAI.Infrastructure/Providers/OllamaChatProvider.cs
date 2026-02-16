using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BlueprintAI.Application.Interfaces;

namespace BlueprintAI.Infrastructure.Providers;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3";
}

public class OllamaChatProvider : IChatProvider
{
    private readonly HttpClient _httpClient;
    private OllamaSettings _settings;

    public string ProviderId => "ollama";
    public bool SupportsNativeToolCalling => true;

    public OllamaChatProvider(HttpClient httpClient, OllamaSettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public void UpdateSettings(OllamaSettings settings) => _settings = settings;

    public async IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(
        List<ChatMessage> messages,
        List<ToolDefinition> tools,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ollama uses OpenAI-compatible API
        var ollamaMessages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var msg in messages)
        {
            if (msg.Role == "user")
                ollamaMessages.Add(new { role = "user", content = msg.Content ?? "" });
            else if (msg.Role == "assistant")
            {
                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    ollamaMessages.Add(new
                    {
                        role = "assistant",
                        content = msg.Content ?? "",
                        tool_calls = msg.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.ArgumentsJson }
                        }).ToArray()
                    });
                }
                else
                {
                    ollamaMessages.Add(new { role = "assistant", content = msg.Content ?? "" });
                }
            }
            else if (msg.Role == "tool")
            {
                ollamaMessages.Add(new
                {
                    role = "tool",
                    tool_call_id = msg.ToolCallId,
                    content = msg.Content ?? ""
                });
            }
        }

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _settings.Model,
            ["messages"] = ollamaMessages,
            ["stream"] = true
        };

        if (tools.Count > 0)
        {
            requestBody["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = JsonSerializer.Deserialize<JsonElement>(t.Schema.RootElement.GetRawText())
                }
            }).ToArray();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var toolCallAccumulators = new Dictionary<int, (string id, string name, StringBuilder args)>();

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]")
            {
                foreach (var (_, (id, name, args)) in toolCallAccumulators)
                {
                    yield return new CompletionChunk
                    {
                        ToolCall = new ToolCallInfo
                        {
                            Id = id, Name = name,
                            ArgumentsJson = args.ToString(),
                            IsComplete = true
                        }
                    };
                }
                yield return new CompletionChunk { IsComplete = true, StopReason = "stop" };
                break;
            }

            JsonDocument doc;
            try { doc = JsonDocument.Parse(data); }
            catch { continue; }

            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) continue;

            var delta = choices[0].GetProperty("delta");
            var finishReason = choices[0].TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;

            if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                yield return new CompletionChunk { TextDelta = content.GetString() };
            }

            if (delta.TryGetProperty("tool_calls", out var tcs))
            {
                foreach (var tc in tcs.EnumerateArray())
                {
                    var index = tc.GetProperty("index").GetInt32();
                    if (tc.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString()!;
                        var name = tc.GetProperty("function").GetProperty("name").GetString()!;
                        toolCallAccumulators[index] = (id, name, new StringBuilder());
                        yield return new CompletionChunk
                        {
                            ToolCall = new ToolCallInfo { Id = id, Name = name, ArgumentsJson = "" }
                        };
                    }

                    if (tc.TryGetProperty("function", out var fn) && fn.TryGetProperty("arguments", out var argDelta))
                    {
                        var argStr = argDelta.GetString() ?? "";
                        if (toolCallAccumulators.TryGetValue(index, out var acc))
                        {
                            acc.args.Append(argStr);
                            yield return new CompletionChunk
                            {
                                ToolCall = new ToolCallInfo
                                {
                                    Id = acc.id, Name = acc.name, ArgumentsJson = argStr
                                }
                            };
                        }
                    }
                }
            }

            if (finishReason == "tool_calls")
            {
                foreach (var (_, (id, name, args)) in toolCallAccumulators)
                {
                    yield return new CompletionChunk
                    {
                        ToolCall = new ToolCallInfo
                        {
                            Id = id, Name = name,
                            ArgumentsJson = args.ToString(),
                            IsComplete = true
                        }
                    };
                }
                toolCallAccumulators.Clear();
            }
        }
    }
}
