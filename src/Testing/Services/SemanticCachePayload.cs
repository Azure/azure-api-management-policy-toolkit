// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal static class SemanticCachePayload
{
    internal static void ValidateEndpoints(GatewayContext context)
    {
        ValidateEndpoint(context.Request.Url.ToUri().AbsolutePath);
        ValidateEndpoint(context.Request.OriginalUrl.ToUri().AbsolutePath);
        if (!string.IsNullOrEmpty(context.BackendUrl))
        {
            var backend = new Uri(context.BackendUrl, UriKind.RelativeOrAbsolute);
            ValidateEndpoint(backend.IsAbsoluteUri ? backend.AbsolutePath : context.BackendUrl.Split('?')[0]);
        }
    }

    internal static void ValidateRequest(GatewayContext context)
    {
        ValidateEndpoints(context);
        using var document = JsonDocument.Parse(context.Request.Body.As<string>(preserveContent: true));
        var root = document.RootElement;
        RequireObject(root);
        SemanticCachePrompt.ValidateUniqueProperties(root);
        ValidateRequest(root);
    }

    internal static void ValidateRequest(JsonElement root)
    {
        ValidateToolOptions(root);
        ValidateMediaFields(root);
        ValidateModalities(root, "modalities", vertex: false);
        if (root.TryGetProperty("stream", out var stream))
        {
            if (stream.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new ArgumentException("The stream request option must be a boolean.");
            }
            if (stream.GetBoolean())
            {
                throw new NotSupportedException("Semantic caching of streaming requests is not supported by the buffered gateway emulator.");
            }
        }

        VisitArray(root, "messages", ValidateMessage);
        if (root.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Array)
        {
            foreach (var message in input.EnumerateArray())
            {
                ValidateMessage(message);
            }
        }
        VisitArray(root, "contents", ValidateVertexMessage);
        if (root.TryGetProperty("systemInstruction", out var instruction))
        {
            ValidateVertexMessage(instruction);
        }
        if (root.TryGetProperty("system", out var system) && system.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in system.EnumerateArray())
            {
                ValidateContentBlock(part);
            }
        }
        if (root.TryGetProperty("generationConfig", out var generation) && generation.ValueKind != JsonValueKind.Null)
        {
            RequireObject(generation);
            ValidateModalities(generation, "responseModalities", vertex: true);
            RejectPresent(generation, "speechConfig", "audio");
            RejectPresent(generation, "imageConfig", "text");
        }
    }

    internal static void ValidateResponse(MockResponse response) => ValidateResponse(response.Body.Content, response.Headers);

    internal static void ValidateResponse(string? content, IReadOnlyDictionary<string, string[]> headers)
    {
        var hasContentType = false;
        var declaredJson = false;
        foreach (var header in headers.Where(header =>
            string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            ArgumentNullException.ThrowIfNull(header.Value);
            foreach (var value in header.Value)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                var mediaType = value.Split(';')[0].Trim();
                hasContentType = true;
                declaredJson |= string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase)
                    || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
                if (string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException("Semantic caching of streaming responses is not supported by the buffered gateway emulator.");
                }
                if (mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                {
                    throw UnsupportedAudio();
                }
                if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    || mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                {
                    throw UnsupportedMedia();
                }
            }
        }

        if (hasContentType && !declaredJson)
        {
            return;
        }
        if (!declaredJson)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }
            var start = content.AsSpan().TrimStart()[0];
            if (start is not ('{' or '['))
            {
                return;
            }
        }

        using var document = JsonDocument.Parse(content ?? string.Empty);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            throw new NotSupportedException("Semantic caching of streaming response chunk arrays is not supported by the buffered gateway emulator.");
        }
        RequireObject(root);
        SemanticCachePrompt.ValidateUniqueProperties(root);
        if (root.TryGetProperty("object", out var discriminator)
            && discriminator.ValueKind == JsonValueKind.String
            && discriminator.GetString() == "chat.completion.chunk")
        {
            throw new NotSupportedException("Semantic caching of streaming completion chunks is not supported by the buffered gateway emulator.");
        }
        ValidateToolOptions(root, allowAutomaticResponseChoice: true);
        ValidateMessage(root);
        RejectToolFinishReason(root, "stop_reason");
        VisitArray(root, "choices", choice =>
        {
            RequireObject(choice);
            RejectToolFinishReason(choice, "finish_reason");
            if (choice.TryGetProperty("message", out var message))
            {
                ValidateMessage(message);
            }
            if (choice.TryGetProperty("delta", out _))
            {
                throw new NotSupportedException("Semantic caching of streaming response deltas is not supported by the buffered gateway emulator.");
            }
        });
        VisitArray(root, "output", ValidateMessage);
        VisitArray(root, "candidates", candidate =>
        {
            RequireObject(candidate);
            if (candidate.TryGetProperty("content", out var message))
            {
                ValidateVertexMessage(message);
            }
        });
    }

    internal static void ValidateMessage(JsonElement message)
    {
        RequireObject(message);
        if (message.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String
            && role.GetString() is "tool" or "function")
        {
            throw UnsupportedTools();
        }
        if (message.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            RejectItemType(type.GetString()!);
        }
        RejectPopulatedArray(message, "tool_calls");
        RejectPresent(message, "function_call", "tool");
        RejectPresent(message, "tool_call_id", "tool");
        RejectPresent(message, "functionCall", "tool");
        RejectPresent(message, "functionResponse", "tool");
        ValidateMediaFields(message);
        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in content.EnumerateArray())
            {
                ValidateContentBlock(part);
            }
        }
    }

    internal static void ValidateContentBlock(JsonElement part)
    {
        RequireObject(part);
        ValidateMediaFields(part);
        RejectPresent(part, "functionCall", "tool");
        RejectPresent(part, "functionResponse", "tool");
        if (part.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            var name = type.GetString()!;
            RejectItemType(name);
            if (name is not ("text" or "input_text" or "output_text" or "refusal" or "thinking" or "redacted_thinking"))
            {
                throw UnsupportedMedia();
            }
        }
    }

    internal static void ValidateVertexTextPart(JsonElement part)
    {
        RequireObject(part);
        if (!part.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String
            || part.EnumerateObject().Any(property => property.Name is not ("text" or "thought" or "thoughtSignature")))
        {
            throw new NotSupportedException(
                "Only text Vertex Parts with thought metadata are supported; media and tool fields are not supported.");
        }
        if (part.TryGetProperty("thought", out var thought)
            && thought.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new ArgumentException("Vertex text Part thought metadata must be a boolean.");
        }
        if (part.TryGetProperty("thoughtSignature", out var signature) && signature.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Vertex text Part thoughtSignature metadata must be a string.");
        }
    }

    private static void ValidateVertexMessage(JsonElement message)
    {
        RequireObject(message);
        ValidateMediaFields(message);
        VisitArray(message, "parts", ValidateVertexTextPart);
    }

    private static void ValidateToolOptions(JsonElement root, bool allowAutomaticResponseChoice = false)
    {
        RejectPopulatedArray(root, "tools");
        RejectPopulatedArray(root, "functions");
        foreach (var name in new[] { "tool_choice", "function_call" })
        {
            if (root.TryGetProperty(name, out var choice) && choice.ValueKind != JsonValueKind.Null
                && (choice.ValueKind != JsonValueKind.String
                    || choice.GetString() != "none"
                    && !(allowAutomaticResponseChoice && name == "tool_choice" && choice.GetString() == "auto")))
            {
                throw UnsupportedTools();
            }
        }
        if (root.TryGetProperty("toolConfig", out var toolConfig) && toolConfig.ValueKind != JsonValueKind.Null)
        {
            RequireObject(toolConfig);
            if (!toolConfig.TryGetProperty("functionCallingConfig", out var calling)
                || calling.ValueKind != JsonValueKind.Object
                || !calling.TryGetProperty("mode", out var mode)
                || mode.ValueKind != JsonValueKind.String || mode.GetString() != "NONE")
            {
                throw UnsupportedTools();
            }
        }
    }

    private static void ValidateMediaFields(JsonElement value)
    {
        foreach (var name in new[] { "audio", "input_audio", "output_audio" })
        {
            RejectPresent(value, name, "audio");
        }
        foreach (var name in new[] { "image_url", "image", "file", "fileData", "inlineData", "video", "videoMetadata", "mediaResolution" })
        {
            RejectPresent(value, name, "text");
        }
    }

    private static void ValidateModalities(JsonElement root, string name, bool vertex)
    {
        if (!root.TryGetProperty(name, out var modalities) || modalities.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        if (modalities.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Semantic cache {name} must be an array of text modalities.");
        }
        foreach (var modality in modalities.EnumerateArray())
        {
            var text = modality.ValueKind == JsonValueKind.String
                && (vertex ? modality.GetString() is "TEXT" or "MODALITY_UNSPECIFIED" : modality.GetString() == "text");
            if (vertex && modality.ValueKind == JsonValueKind.Number && modality.TryGetInt32(out var numeric))
            {
                text = numeric is 0 or 1;
            }
            if (!text)
            {
                throw new NotSupportedException("Semantic cache supports text output only; audio and other modalities are not supported.");
            }
        }
    }

    private static void RejectPopulatedArray(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var items) || items.ValueKind == JsonValueKind.Null)
        {
            return;
        }
        if (items.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Semantic cache {name} must be an array.");
        }
        if (items.GetArrayLength() != 0)
        {
            throw UnsupportedTools();
        }
    }

    private static void RejectPresent(JsonElement value, string name, string feature)
    {
        if (value.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null)
        {
            throw feature switch
            {
                "tool" => UnsupportedTools(),
                "audio" => UnsupportedAudio(),
                _ => UnsupportedMedia()
            };
        }
    }

    private static void RejectItemType(string type)
    {
        if (type.Contains("tool", StringComparison.Ordinal) || type.EndsWith("_call", StringComparison.Ordinal)
            || type.EndsWith("_call_output", StringComparison.Ordinal))
        {
            throw UnsupportedTools();
        }
        if (type.Contains("audio", StringComparison.Ordinal))
        {
            throw UnsupportedAudio();
        }
        if (type.Contains("image", StringComparison.Ordinal) || type is "file" or "input_file" or "video")
        {
            throw UnsupportedMedia();
        }
    }

    private static void RejectToolFinishReason(JsonElement value, string name)
    {
        if (value.TryGetProperty(name, out var reason) && reason.ValueKind == JsonValueKind.String
            && reason.GetString() is "tool_calls" or "function_call" or "tool_use")
        {
            throw UnsupportedTools();
        }
    }

    private static void ValidateEndpoint(string path)
    {
        var decoded = Uri.UnescapeDataString(path).TrimEnd('/');
        if (decoded.EndsWith(":streamGenerateContent", StringComparison.OrdinalIgnoreCase)
            || decoded.EndsWith(":streamRawPredict", StringComparison.OrdinalIgnoreCase)
            || decoded.EndsWith(":serverStreamingPredict", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Semantic caching of streaming RPC endpoints is not supported by the buffered gateway emulator.");
        }
    }

    private static void VisitArray(JsonElement parent, string name, Action<JsonElement> visit)
    {
        if (!parent.TryGetProperty(name, out var items))
        {
            return;
        }
        if (items.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Semantic cache {name} must be an array.");
        }
        foreach (var item in items.EnumerateArray())
        {
            visit(item);
        }
    }

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Semantic cache payloads and dialog items must be JSON objects.");
        }
    }

    private static NotSupportedException UnsupportedTools() =>
        new("Semantic cache emulator does not support tool-enabled requests, tool-call history, or tool-call responses.");

    private static NotSupportedException UnsupportedAudio() =>
        new("Semantic cache emulator supports text only; audio requests, references, and responses are not supported.");

    private static NotSupportedException UnsupportedMedia() =>
        new("Semantic cache emulator supports text only; media fields and responses are not supported.");
}