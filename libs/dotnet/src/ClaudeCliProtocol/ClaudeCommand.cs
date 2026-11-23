using System;
using System.Collections.Generic;

namespace ClaudeCliProtocol
{
    /// <summary>
    /// Something to send to the claude: a message, an answer to a permission request or a control request. A command
    /// is only data; a transport delivers it in its own way.
    /// </summary>
    public abstract record ClaudeCommand;

    /// <summary>
    /// A user turn. A file is named in <paramref name="Text"/> by a line of its own
    /// (<see cref="AttachedFileLine.Format"/>); whoever builds the command adds those lines.
    /// </summary>
    public sealed record SendUserMessage(string Text) : ClaudeCommand
    {
        /// <summary>The images sent with the text; empty when there are none.</summary>
        public IReadOnlyList<ImageData> Images { get; init; } = Array.Empty<ImageData>();

        /// <summary>
        /// An id for the message. With it the claude tells what became of the message (<c>command_lifecycle</c>,
        /// <see cref="CommandLifecycle"/>); null sends none.
        /// </summary>
        public string? Uuid { get; init; }

        /// <summary>The long pastes still in <see cref="Text"/>, written as <c>inline_pastes</c>; empty writes none.</summary>
        public IReadOnlyList<string> InlinePastes { get; init; } = Array.Empty<string>();
    }

    /// <summary>The answer to a permission request.</summary>
    public enum PermissionDecision
    {
        Allow,
        AllowAlways,
        Deny,
    }

    /// <summary>
    /// The answer to a <c>can_use_tool</c> request (<see cref="PermissionRequested"/>). It names the request, the
    /// decision and, for a <see cref="PermissionDecision.Deny"/>, what to tell the claude. It carries neither the
    /// tool's input nor the permission suggestions: whoever writes the answer to the claude's stdin takes both from
    /// the request it received from the claude itself (<see cref="StdinWriter.Write(AnswerPermission, PermissionRequested)"/>).
    /// </summary>
    public sealed record AnswerPermission(string RequestId, PermissionDecision Decision, string? Message = null) : ClaudeCommand;

    /// <summary>
    /// A control request. <see cref="RequestId"/> is set by whoever sends the request (a session does it); a caller
    /// leaves it null.
    /// </summary>
    public abstract record ControlCommand : ClaudeCommand
    {
        /// <summary>The id the claude repeats in its reply (<see cref="ControlAcknowledged.RequestId"/>).</summary>
        public string? RequestId { get; init; }

        /// <summary>The <c>subtype</c> of the request, as the claude names it.</summary>
        public abstract string Subtype { get; }
    }

    /// <summary>The handshake the official SDKs send first; its reply lists the commands, the models and the account.</summary>
    public sealed record Initialize : ControlCommand
    {
        public override string Subtype => "initialize";
    }

    /// <summary>Stops the turn. The reply tells the messages still queued (<c>still_queued</c>).</summary>
    public sealed record Interrupt : ControlCommand
    {
        public override string Subtype => "interrupt";
    }

    /// <summary>Asks for a permission mode (<c>plan</c>, <c>default</c>, <c>acceptEdits</c>, <c>auto</c>…); it holds once the reply is a success.</summary>
    public sealed record SetPermissionMode(string Mode) : ControlCommand
    {
        public override string Subtype => "set_permission_mode";
    }

    /// <summary>Asks for a model; it holds once the reply is a success.</summary>
    public sealed record SetModel(string Model) : ControlCommand
    {
        public override string Subtype => "set_model";
    }

    /// <summary>
    /// Sets the effort level for the session; null clears it (back to the model's default). The reply confirms
    /// nothing: the level in force comes in the reply to <see cref="GetSettings"/>.
    /// </summary>
    public sealed record ApplyEffort(string? Effort) : ControlCommand
    {
        public override string Subtype => "apply_flag_settings";
    }

    /// <summary>Asks for the settings in force (the model and the effort).</summary>
    public sealed record GetSettings : ControlCommand
    {
        public override string Subtype => "get_settings";
    }

    /// <summary>Asks how the context window is used.</summary>
    public sealed record GetContextUsage : ControlCommand
    {
        public override string Subtype => "get_context_usage";
    }

    /// <summary>Asks what the command the claude runs as the task <paramref name="TaskId"/> has printed so far.</summary>
    public sealed record GetTaskOutput(string TaskId) : ControlCommand
    {
        public override string Subtype => "get_task_output";
    }

    /// <summary>Turns Remote Control on or off; the reply to turning it on brings the session's link.</summary>
    public sealed record SetRemoteControl(bool Enabled) : ControlCommand
    {
        public override string Subtype => "remote_control";
    }

    /// <summary>Asks for the state of the browser extension.</summary>
    public sealed record GetChromeStatus : ControlCommand
    {
        public override string Subtype => "get_chrome_dialog";
    }
}
