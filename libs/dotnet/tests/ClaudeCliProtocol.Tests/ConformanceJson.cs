using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ClaudeCliProtocol.Tests
{
    /// <summary>
    /// Writes events in the neutral format of the expected results (fixtures/README.md), version 1. The format is
    /// the contract with the libraries of other languages; this writer is how the .NET library meets it.
    /// </summary>
    internal static class ConformanceJson
    {
        public const int Format = 1;

        private static readonly JsonWriterOptions Options =
            new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private static readonly Dictionary<Type, string> BlockKinds = new Dictionary<Type, string>
        {
            [typeof(TextContentBlock)] = "text",
            [typeof(ThinkingBlock)] = "thinking",
            [typeof(ToolUseBlock)] = "tool_use",
            [typeof(ToolResultBlock)] = "tool_result",
            [typeof(ImageBlock)] = "image",
        };

        /// <summary>The whole document: one event per line of the file, so a change shows as one changed line.</summary>
        public static string Write(IEnumerable<(int Line, ClaudeEvent Event)> events)
        {
            var text = new StringBuilder();
            text.Append("{\n  \"format\": ").Append(Format.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"events\": [");
            var any = false;
            foreach (var (line, e) in events)
            {
                text.Append(any ? ",\n    " : "\n    ").Append(Event(line, e));
                any = true;
            }
            text.Append(any ? "\n  ]\n}\n" : "]\n}\n");
            return text.ToString();
        }

        private static string Event(int line, ClaudeEvent e)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, Options))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("line", line);
                    writer.WriteString("event", SnakeCase(e.GetType().Name));
                    writer.WritePropertyName("fields");
                    writer.WriteStartObject();
                    Fields(writer, e);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>Every public property with a setter (constructor parameters and init properties), in snake_case.</summary>
        private static void Fields(Utf8JsonWriter writer, object owner)
        {
            var properties = owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .OrderBy(p => p.MetadataToken);
            foreach (var property in properties)
            {
                writer.WritePropertyName(SnakeCase(property.Name));
                var value = property.GetValue(owner);
                if (owner is ImageBlock && property.Name == nameof(ImageBlock.Data) && value is string data)
                {
                    // an image does not go in whole: its size and its hash say whether it is the same
                    writer.WriteStartObject();
                    writer.WriteNumber("length", data.Length);
                    writer.WriteString("sha256", Sha256(data));
                    writer.WriteEndObject();
                }
                else
                {
                    Value(writer, value);
                }
            }
        }

        private static void Value(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case bool flag:
                    writer.WriteBooleanValue(flag);
                    break;
                case int number:
                    writer.WriteNumberValue(number);
                    break;
                case long number:
                    writer.WriteNumberValue(number);
                    break;
                case double number:
                    writer.WriteNumberValue(number);
                    break;
                case Enum name:
                    writer.WriteStringValue(SnakeCase(name.ToString()));
                    break;
                case TimeSpan span:
                    writer.WriteNumberValue((long)Math.Round(span.TotalMilliseconds));
                    break;
                case DateTimeOffset time:
                    writer.WriteStringValue(time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                    break;
                case JsonElement element:
                    if (element.ValueKind == JsonValueKind.Undefined) writer.WriteNullValue();
                    else element.WriteTo(writer);
                    break;
                case ContentBlock block:
                    writer.WriteStartObject();
                    writer.WriteString("kind", BlockKinds.TryGetValue(block.GetType(), out var kind)
                        ? kind
                        : throw new NotSupportedException("No kind for the content block " + block.GetType().Name));
                    Fields(writer, block);
                    writer.WriteEndObject();
                    break;
                case IEnumerable items:
                    writer.WriteStartArray();
                    foreach (var item in items) Value(writer, item);
                    writer.WriteEndArray();
                    break;
                default:
                    {
                        var type = value.GetType();
                        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("key");
                            Value(writer, type.GetProperty("Key")!.GetValue(value));
                            writer.WritePropertyName("value");
                            Value(writer, type.GetProperty("Value")!.GetValue(value));
                            writer.WriteEndObject();
                        }
                        else if (type.Assembly == typeof(ClaudeEvent).Assembly && type.IsClass)
                        {
                            writer.WriteStartObject();
                            Fields(writer, value);
                            writer.WriteEndObject();
                        }
                        else
                        {
                            throw new NotSupportedException("The format has no rule for a value of type " + type.FullName);
                        }
                        break;
                    }
            }
        }

        /// <summary><c>AssistantBlock</c> gives <c>assistant_block</c>; <c>CostUsd</c> gives <c>cost_usd</c>.</summary>
        public static string SnakeCase(string name)
        {
            var text = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c) && i > 0
                    && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    text.Append('_');
                text.Append(char.ToLowerInvariant(c));
            }
            return text.ToString();
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Where two documents first differ, or null when they say the same: the order of an object's keys does not
        /// count, the order of an array's items does, and numbers are compared by value.
        /// </summary>
        public static string? FirstDifference(JsonElement expected, JsonElement actual, string path = "$")
        {
            if (expected.ValueKind == JsonValueKind.Number && actual.ValueKind == JsonValueKind.Number)
                return Math.Abs(expected.GetDouble() - actual.GetDouble()) <= 1e-9
                    ? null
                    : path + ": expected " + expected.GetRawText() + ", actual " + actual.GetRawText();
            if (expected.ValueKind != actual.ValueKind)
                return path + ": expected " + Short(expected) + ", actual " + Short(actual);
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var wanted = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                        var got = actual.EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
                        foreach (var name in wanted.Keys.Where(n => !got.ContainsKey(n))) return path + "." + name + ": missing";
                        foreach (var name in got.Keys.Where(n => !wanted.ContainsKey(n))) return path + "." + name + ": not expected";
                        foreach (var pair in wanted)
                            if (FirstDifference(pair.Value, got[pair.Key], path + "." + pair.Key) is { } difference) return difference;
                        return null;
                    }
                case JsonValueKind.Array:
                    {
                        if (expected.GetArrayLength() != actual.GetArrayLength())
                            return path + ": expected " + expected.GetArrayLength() + " items, actual " + actual.GetArrayLength();
                        for (var i = 0; i < expected.GetArrayLength(); i++)
                            if (FirstDifference(expected[i], actual[i], path + "[" + i + "]") is { } difference) return difference;
                        return null;
                    }
                case JsonValueKind.String:
                    return expected.GetString() == actual.GetString()
                        ? null
                        : path + ": expected " + Short(expected) + ", actual " + Short(actual);
                default:
                    return null; // true, false and null have no value beyond their kind
            }
        }

        private static string Short(JsonElement element)
        {
            var raw = element.ValueKind == JsonValueKind.Undefined ? "(nothing)" : element.GetRawText();
            return raw.Length <= 120 ? raw : raw.Substring(0, 120) + "…";
        }
    }
}
