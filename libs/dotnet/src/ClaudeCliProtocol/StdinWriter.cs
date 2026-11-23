using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// Writes the lines for the stdin of <c>claude -p --input-format stream-json</c>. Each one is a single JSON line
    /// without the trailing line feed. Text outside ASCII is escaped (<c>ã</c>), so the line reads the same
    /// whatever encoding the host gives the process stdin.
    /// </summary>
    public static class StdinWriter
    {
        public const string DefaultDenyMessage = "The user denied this tool use.";

        /// <summary>
        /// A user turn. Without images the content is the plain text; with images it is an array of the image blocks
        /// followed by the text block (left out when the text is empty).
        /// </summary>
        public static string Write(SendUserMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var text = message.Text ?? throw new ArgumentNullException(nameof(message), "The message has no text.");
            var images = message.Images;
            var inlinePastes = message.InlinePastes;
            return Line(w =>
            {
                w.WriteString("type", "user");
                w.WriteStartObject("message");
                w.WriteString("role", "user");
                if (images == null || images.Count == 0)
                {
                    w.WriteString("content", text);
                }
                else
                {
                    w.WriteStartArray("content");
                    foreach (var image in images)
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "image");
                        w.WriteStartObject("source");
                        w.WriteString("type", "base64");
                        w.WriteString("media_type", image.MediaType);
                        w.WriteString("data", image.Data);
                        w.WriteEndObject();
                        w.WriteEndObject();
                    }
                    if (text.Length > 0)
                    {
                        w.WriteStartObject();
                        w.WriteString("type", "text");
                        w.WriteString("text", text);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
                w.WriteNull("parent_tool_use_id");
                w.WriteString("session_id", "default");
                if (message.Uuid != null) w.WriteString("uuid", message.Uuid);
                if (inlinePastes != null && inlinePastes.Count > 0)
                {
                    w.WriteStartArray("inline_pastes");
                    foreach (var paste in inlinePastes) w.WriteStringValue(paste);
                    w.WriteEndArray();
                }
            });
        }

        /// <summary>A control request. The command must have its <see cref="ControlCommand.RequestId"/>.</summary>
        public static string Write(ControlCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var requestId = command.RequestId
                ?? throw new ArgumentException("A control command needs a request id before it is written.", nameof(command));
            if (command is GetTaskOutput { TaskId: null })
                throw new ArgumentNullException(nameof(command), "The task id is null.");
            return Line(w =>
            {
                w.WriteString("type", "control_request");
                w.WriteString("request_id", requestId);
                w.WriteStartObject("request");
                w.WriteString("subtype", command.Subtype);
                switch (command)
                {
                    case SetPermissionMode mode:
                        w.WriteString("mode", mode.Mode);
                        break;
                    case SetModel model:
                        w.WriteString("model", model.Model);
                        break;
                    case ApplyEffort effort:
                        w.WriteStartObject("settings");
                        if (effort.Effort == null) w.WriteNull("effortLevel");
                        else w.WriteString("effortLevel", effort.Effort);
                        w.WriteEndObject();
                        break;
                    case GetTaskOutput output:
                        w.WriteString("task_id", output.TaskId);
                        break;
                    case SetRemoteControl remote:
                        w.WriteBoolean("enabled", remote.Enabled);
                        break;
                }
                w.WriteEndObject();
            });
        }

        /// <summary>
        /// The answer to a <c>can_use_tool</c> request. The input and the suggestions are those of
        /// <paramref name="request"/>, the request the process side received from the claude, never the caller's:
        /// <c>allow</c> sends the input back unchanged, "allow always" also sends the request's suggestions as
        /// <c>updatedPermissions</c>, and <c>deny</c> sends the message.
        /// </summary>
        public static string Write(AnswerPermission answer, PermissionRequested request)
        {
            if (answer == null) throw new ArgumentNullException(nameof(answer));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!string.Equals(answer.RequestId, request.RequestId, StringComparison.Ordinal))
                throw new ArgumentException("The answer names another request.", nameof(answer));
            return Line(w =>
            {
                w.WriteString("type", "control_response");
                w.WriteStartObject("response");
                w.WriteString("subtype", "success");
                w.WriteString("request_id", request.RequestId);
                w.WriteStartObject("response");
                if (answer.Decision == PermissionDecision.Deny)
                {
                    w.WriteString("behavior", "deny");
                    w.WriteString("message", string.IsNullOrWhiteSpace(answer.Message) ? DefaultDenyMessage : answer.Message);
                }
                else
                {
                    w.WriteString("behavior", "allow");
                    w.WritePropertyName("updatedInput");
                    if (request.Input.ValueKind == JsonValueKind.Undefined)
                    {
                        w.WriteStartObject();
                        w.WriteEndObject();
                    }
                    else
                    {
                        request.Input.WriteTo(w);
                    }
                    if (answer.Decision == PermissionDecision.AllowAlways && request.Suggestions is { } suggestions
                        && suggestions.ValueKind == JsonValueKind.Array)
                    {
                        w.WritePropertyName("updatedPermissions");
                        suggestions.WriteTo(w);
                    }
                }
                w.WriteEndObject();
                w.WriteEndObject();
            });
        }

        private static string Line(Action<Utf8JsonWriter> body)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    body(writer);
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
