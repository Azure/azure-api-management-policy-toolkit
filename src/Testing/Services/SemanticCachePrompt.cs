// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Services;

internal sealed record SemanticCachePrompt(string Content, string Parameters, string Schema, int MessageCount)
{
    internal static SemanticCachePrompt Create(GatewayContext context, bool ignoreSystemMessages)
    {
        SemanticCachePayload.ValidateEndpoints(context);
        using var document = JsonDocument.Parse(context.Request.Body.As<string>(preserveContent: true));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Semantic cache requests must be JSON objects containing dialog messages.");
        }

        ValidateUniqueProperties(root);
        SemanticCachePayload.ValidateRequest(root);

        var messages = new List<Dictionary<string, object?>>();
        var hasMessages = root.TryGetProperty("messages", out var dialog);
        var hasInput = root.TryGetProperty("input", out var input);
        var hasContents = root.TryGetProperty("contents", out var contents);
        if ((hasMessages ? 1 : 0) + (hasInput ? 1 : 0) + (hasContents ? 1 : 0) > 1)
        {
            throw new ArgumentException("A semantic cache request must use exactly one dialog schema.");
        }

        string schema;
        string[] excluded;
        if (hasMessages)
        {
            schema = "messages";
            excluded = ["messages", "system"];
            if (root.TryGetProperty("system", out var system) && system.ValueKind != JsonValueKind.Null)
            {
                messages.Add(Message("system", ReadText(system)));
            }
            ReadMessages(dialog, messages);
        }
        else if (hasInput)
        {
            schema = "responses";
            excluded = ["input", "instructions"];
            if (root.TryGetProperty("instructions", out var instructions) && instructions.ValueKind != JsonValueKind.Null)
            {
                messages.Add(Message("system", ReadText(instructions)));
            }
            if (input.ValueKind == JsonValueKind.String)
            {
                messages.Add(Message("user", ReadText(input)));
            }
            else
            {
                ReadMessages(input, messages);
            }
        }
        else if (hasContents)
        {
            schema = "vertex";
            excluded = ["contents", "systemInstruction"];
            if (root.TryGetProperty("systemInstruction", out var instruction))
            {
                messages.Add(VertexMessage("system", instruction));
            }
            RequireArray(contents);
            foreach (var message in contents.EnumerateArray())
            {
                RequireObject(message);
                var role = message.TryGetProperty("role", out var roleValue) ? ReadRole(roleValue) : "user";
                if (role == "model")
                {
                    role = "assistant";
                }
                messages.Add(VertexMessage(role, message));
            }
        }
        else
        {
            throw new NotSupportedException(
                "Unsupported semantic cache request schema. Use a text-only Chat Completions, Responses, " +
                "Anthropic Messages, or Vertex contents envelope.");
        }

        if (ignoreSystemMessages)
        {
            messages.RemoveAll(message => Equals(message["role"], "system"));
        }
        if (messages.Count == 0)
        {
            throw new ArgumentException("Semantic cache lookup requires at least one remaining dialog message.");
        }

        var parameters = root.EnumerateObject()
            .Where(property => !excluded.Contains(property.Name, StringComparer.Ordinal))
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        return new SemanticCachePrompt(Canonicalize(messages), Canonicalize(parameters), schema, messages.Count);
    }

    internal static string Canonicalize(object value) => Canonicalize(JsonSerializer.SerializeToElement(value));

    private static string Canonicalize(JsonElement value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCanonical(writer, value);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void ReadMessages(JsonElement dialog, List<Dictionary<string, object?>> messages)
    {
        RequireArray(dialog);
        foreach (var message in dialog.EnumerateArray())
        {
            RequireObject(message);
            var hasToolType = message.TryGetProperty("type", out var type)
                && (type.ValueKind != JsonValueKind.String || type.GetString() != "message");
            if (hasToolType)
            {
                throw new NotSupportedException(
                    "Semantic caching of tool-call turns requires tool execution state that the text-only emulator cannot model.");
            }
            if (!message.TryGetProperty("role", out var roleValue)
                || !message.TryGetProperty("content", out var content))
            {
                throw new ArgumentException("Every semantic cache dialog message requires role and content.");
            }

            var role = ReadRole(roleValue);
            var normalized = Message(role, ReadText(content));
            foreach (var property in message.EnumerateObject()
                .Where(property => property.Name is not ("role" or "content" or "type")))
            {
                normalized[property.Name] = property.Value.Clone();
            }
            messages.Add(normalized);
        }
    }

    private static string ReadRole(JsonElement role)
    {
        if (role.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(role.GetString()))
        {
            throw new ArgumentException("Semantic cache message roles must be nonempty strings.");
        }

        var name = role.GetString()!;
        if (name is "tool" or "function")
        {
            throw new NotSupportedException("Semantic caching of tool-call turns is not supported by the text-only emulator.");
        }
        if (name is not ("system" or "developer" or "user" or "assistant" or "model"))
        {
            throw new ArgumentException($"Unsupported semantic cache message role '{name}'.");
        }
        return name;
    }

    private static string ReadText(JsonElement content)
    {
        string? text;
        if (content.ValueKind == JsonValueKind.String)
        {
            text = content.GetString();
        }
        else if (content.ValueKind == JsonValueKind.Array)
        {
            var parts = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                RequireObject(part);
                if (!part.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
                    || type.GetString() is not ("text" or "input_text" or "output_text"))
                {
                    throw new NotSupportedException(
                        "Only text content can be embedded by the local semantic cache; multimodal content is not supported.");
                }
                if (!part.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String)
                {
                    throw new ArgumentException("Semantic cache text content blocks require a text string.");
                }
                parts.Append(value.GetString());
            }
            text = parts.ToString();
        }
        else
        {
            throw new ArgumentException("Semantic cache message content must be a string or an array of text blocks.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Semantic cache dialog message text must not be empty.");
        }
        return text;
    }

    private static string ReadVertexText(JsonElement message)
    {
        RequireObject(message);
        if (!message.TryGetProperty("parts", out var parts))
        {
            throw new ArgumentException("Vertex semantic cache messages require text parts.");
        }
        RequireArray(parts);
        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            SemanticCachePayload.ValidateVertexTextPart(part);
            var value = part.GetProperty("text");
            text.Append(value.GetString());
        }
        var content = text.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Vertex semantic cache message text must not be empty.");
        }
        return content;
    }

    private static Dictionary<string, object?> Message(string role, string content) =>
        new(StringComparer.Ordinal) { ["role"] = role, ["content"] = content };

    private static Dictionary<string, object?> VertexMessage(string role, JsonElement message)
    {
        var normalized = Message(role, ReadVertexText(message));
        var parts = message.GetProperty("parts");
        if (parts.EnumerateArray().Any(part => part.EnumerateObject().Any(property => property.Name != "text")))
        {
            normalized["parts"] = parts.Clone();
        }
        return normalized;
    }

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Semantic cache dialog messages and content blocks must be JSON objects.");
        }
    }

    private static void RequireArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
        {
            throw new ArgumentException("Semantic cache dialog messages and content blocks must be nonempty arrays.");
        }
    }

    internal static void ValidateUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new ArgumentException($"Semantic cache request contains duplicate JSON property '{property.Name}'.");
                }
                ValidateUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                ValidateUniqueProperties(item);
            }
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}