---
title: Events
description: The table of the events the readers produce — which line each one comes from, the fields, and which ones no recording exercises.
---

A reader turns each line into none, one or more typed events. This page is their reference: the
name, the line it comes from and the fields. **The names are those of the expected result format**
(see [Fixtures](../fixtures/#the-expected-result)), in `snake_case`, and not those of the stream:
the `cwd` of the `system/init` line is the `working_directory` of `session_started`. A library in
another language may give its types other names, but the expected result it produces uses these.

Rules that hold for all of them:

- **The set of fields is fixed.** A field with no value is `null`; it does not disappear.
- **The values passed through** (the `input` of a `tool_use` and of a `permission_requested`, the
  `suggestions`, the `tool_use_result` and the `response` of a `control_acknowledged`) go as
  `claude` sent them, with its key names.
- **Live reader and file reader.** "stdout" means the reader of the lines of `claude -p`; "file"
  means the reader of the session file (`.jsonl`). Some events come from both. The "Comes from"
  column says where the line actually shows up: the `system` lines of tasks, hooks, `api_retry`,
  `thinking_tokens`, `commands_changed`, `bridge_state` and `local_command` are read by the same
  code in both readers, and yield an event from the file too when the file has them. Those of
  `init`, `status`, `per_turn_effort_changed`, `model_refusal_fallback` and `peer_message_hold` are
  stdout only.

## The session and the turn

| Event | Comes from | Fields |
|---|---|---|
| `session_started` | stdout: `system/init` | `session_id`, `model`, `working_directory`, `permission_mode`, `claude_version`, `terminal_commands`, `fast_mode_state`, `ultrathink_available` |
| `status_changed` | stdout: `system/status` | `status`, `permission_mode` |
| `turn_completed` | stdout: `result` | `subtype`, `is_error`, `duration_ms`, `cost_usd`, `input_tokens`, `output_tokens`, `api_duration_ms`, `error_message`, `fast_mode_state` |
| `system_notice` | stdout and file: `system/compact_boundary`, `system/api_retry` | `subtype`, `trigger`, `pre_tokens`, `post_tokens`, `error_message` |
| `informational` | stdout: `system/informational` | `level`, `text` |
| `thinking_tokens` | stdout: `system/thinking_tokens` | `estimated_tokens` |
| `prompt_cache_used` | stdout and file: the `usage` of an `assistant` line of the main conversation | `ttl`, `at` |

- `turn_completed.input_tokens` adds `input_tokens`, `cache_creation_input_tokens` and
  `cache_read_input_tokens`. `cost_usd` is `total_cost_usd`.
- `prompt_cache_used.ttl` is 1 h or 5 min, from `cache_creation` (`ephemeral_1h_input_tokens` or
  `ephemeral_5m_input_tokens`); `null` when the reply only read the cache.
- `informational.level` is `info`, `warning` or `error` (`info` when the line does not say). A hook
  that blocked the prompt sends a `warning` (`hook-blocked` scenario).

## The reply as it arrives

| Event | Comes from | Fields |
|---|---|---|
| `message_started` | stdout: `stream_event` / `message_start` | `message_id`, `parent_tool_use_id` |
| `block_started` | stdout: `stream_event` / `content_block_start` | `message_id`, `index`, `parent_tool_use_id`, `block` |
| `block_delta` | stdout: `stream_event` / `content_block_delta` | `message_id`, `index`, `parent_tool_use_id`, `kind`, `text`, `estimated_tokens` |
| `block_stopped` | stdout: `stream_event` / `content_block_stop` | `message_id`, `index`, `parent_tool_use_id` |

- The `message_id` of the last three is that of the last `message_start` of the same
  `parent_tool_use_id`; empty if there was none.
- `block_delta.kind` is `text`, `thinking` or `input_json`.
- The `block` of a `block_started` of `tool_use` has an empty `input` (`{}`).

## The messages

| Event | Comes from | Fields |
|---|---|---|
| `assistant_block` | stdout and file: `assistant`, one per block | `message_id`, `parent_tool_use_id`, `block`, `timestamp`, `thinking_duration_ms`, `uuid`, `supersedes`, `is_synthetic` |
| `user_block` | stdout and file: `user` (and `attachment` of `queued_command`, in the file) | `parent_tool_use_id`, `block`, `tool_use_result`, `is_meta`, `is_compact_summary`, `timestamp`, `images`, `is_synthetic`, `origin`, `uuid`, `is_queued_command` |
| `command_echo` | file: the `<command-name>` text of a `user` line | `name`, `arguments` |
| `command_output` | stdout and file: the `<local-command-stdout>` text; `system/local_command` | `text` |

- `assistant_block.is_synthetic` is true when `message.model` is `<synthetic>`.
- `assistant_block.supersedes` are the `uuid`s of the lines this one replaces (a reply `claude`
  discarded and wrote again).
- `user_block.origin` says where the message came from: `kind` (`human`, `peer`, `channel`,
  `task-notification`, `coordinator`, `plugin`…), `name`, `sender_task_id` and `subkind`.
- `command_echo.name` comes without the slash.

### The blocks

The `block` field is an object with `kind` and the fields of the block:

| `kind` | Fields |
|---|---|
| `text` | `text` |
| `thinking` | `text` |
| `tool_use` | `id`, `name`, `input` |
| `tool_result` | `tool_use_id`, `text`, `is_error`, `non_execution_kind`, `images` |
| `image` | `media_type`, `data` |

The `text` of a `tool_result` joins the text blocks of the `content` with `\n`. The `data` of an
image goes as `{"length": …, "sha256": …}` in the expected result, not whole.

## Permission and control

| Event | Comes from | Fields |
|---|---|---|
| `permission_requested` | stdout: `control_request` / `can_use_tool` | `request_id`, `tool_name`, `display_name`, `description`, `tool_use_id`, `input`, `suggestions`, `agent_id`, `default_to_no`, `suppress_always_allow_rule`, `decision_reason`, `decision_reason_type`, `decision_reason_code`, `mcp_server_source` |
| `permission_cancelled` | stdout: `control_cancel_request` | `request_id` |
| `control_acknowledged` | stdout: `control_response` | `request_id`, `is_error`, `response`, `error` |
| `commands_available` | stdout: the body of a `control_response` with `commands`; `system/commands_changed` | `commands` |
| `models_available` | stdout: the body of a `control_response` with `models` | `models` |
| `auth_conflict_reported` | stdout: the body of a `control_response` with `commands` and `models` or `account` | `conflict` |
| `settings_applied` | stdout: the body of a `control_response` with `applied` | `model`, `effort`, `has_effort`, `ultrathink_available` |
| `context_usage_reported` | stdout: the body of a `control_response` with `categories` and `totalTokens` | `usage` |

- A `control_response` gives a `control_acknowledged` and, after it, the events its body has, in
  the order of the table in [Control](../controle/#how-the-body-is-read).
- `commands[]`: `name`, `description`, `argument_hint`.
- `models[]`: `value`, `resolved_model`, `display_name`, `description`, `effort_levels`.
- `usage`: `total_tokens`, `max_tokens`, `percentage` (an integer, rounded), `categories[]`
  (`name`, `tokens`, `kind`), `model`, `memory_files[]` (`path`, `tokens`) and `agents[]` (`name`,
  `tokens`).
- `conflict`: `token_source` and `api_key_source`, or `null` when there is no conflict.
- `settings_applied.has_effort` is false when `applied` has no `effort`.

## Background tasks

| Event | Comes from | Fields |
|---|---|---|
| `agent_task_started` | stdout: `system/task_started` | `task_id`, `tool_use_id`, `description`, `agent_type`, `is_background`, `depth`, `task_type`, `prompt` |
| `agent_task_progress` | stdout: `system/task_progress` | `task_id`, `tool_use_id`, `activity`, `tokens`, `tool_uses`, `duration_ms` |
| `agent_task_updated` | stdout: `system/task_updated` | `task_id`, `status`, `is_backgrounded` |
| `agent_task_finished` | stdout: `system/task_notification`; file: the `<task-notification>` text of a `user` line | `task_id`, `tool_use_id`, `status`, `summary`, `tokens`, `tool_uses`, `duration_ms` |

- `task_type` is `local_agent` for a sub-agent and `local_bash` for a command.
- `depth` is the `spawn_depth` (1 when the line does not say).
- `tokens`, `tool_uses` and `duration_ms` come from the `usage` of the line.

## The ones no recording exercises

These events come from lines the 42 recordings do not have. Their shape is in the code of the .NET
library and in the unit tests, and it is what is here. When a recording brings them, this table is
checked against it.

| Event | Comes from | Fields |
|---|---|---|
| `hook_started` | stdout and file: `system/hook_started` | `hook_id`, `hook_event` |
| `hook_finished` | stdout and file: `system/hook_response` | `hook_id` |
| `command_lifecycle` | stdout: `command_lifecycle` | `command_uuid`, `state`, `ran_ahead` |
| `per_turn_effort_changed` | stdout: `system/per_turn_effort_changed` | none |
| `usage_limits_reported` | stdout: `rate_limit_event`; the body of a `control_response` with `rate_limits` | `limits` |
| `account_reported` | stdout: the body of a `control_response` with `account` | `account` |
| `remote_control_changed` | stdout and file: `system/bridge_state`; the body of a `control_response` with `session_url` | `state`, `url`, `detail` |
| `model_refusal_fallback` | stdout: `system/model_refusal_fallback` | `original_model`, `fallback_model`, `category` |
| `peer_message_hold` | stdout: `system/peer_message_hold` | `state`, `from`, `message_uuid`, `from_name`, `cause`, `outcome` |

- `hook_started.hook_event` is the event the hook serves: `SessionStart`, `Setup`, `PreToolUse`…
- `command_lifecycle.state` is `queued`, `started`, `completed`, `cancelled`, `discarded` or
  `refused` (see [Commands](../comandos/#with-uuid)).
- `per_turn_effort_changed` says that the word `ultrathink` in a prompt raised the effort of the
  turn.
- `limits`: `windows[]` (`name`, `utilization`, `resets_at`, `model`) and `is_snapshot`. A
  `rate_limit_event` carries only the windows that changed, with `utilization` from 0 to 1 and
  `resetsAt` in Unix seconds; the answer to a `get_usage` is a whole snapshot (`is_snapshot` true).
- `account`: `auth_method` (`claudeai`, `api-key`, `bedrock`, `vertex`, `foundry`, `gateway` or
  `third-party`), `email`, `organization` and `plan` (the name of the plan, such as "Max").
- `model_refusal_fallback`: `claude`'s safeguards switched the model, and the reply comes from
  `fallback_model`.
- `peer_message_hold.state` is `held`, `released` or `dropped`: a message from another session that
  `claude` held back for the user to review.

## The session's event

| Event | Comes from | Fields |
|---|---|---|
| `control_replied` | `ClaudeSession`, not a reader: the answer to a request it sent | `command`, `reply` |

It joins the request the session sent (`command`) to the `control_acknowledged` that answered it
(`reply`), by the `request_id`. It does not show up in the expected result, because it does not come
from a line.

## Lines that yield no event

- `stream_event` of `message_delta` and of `message_stop`;
- `system/background_tasks_changed`;
- `system/model_refusal_no_fallback`;
- any `control_request` that is not `can_use_tool` (the transport answers with an error, see
  [Control](../controle/#the-request-nobody-serves));
- every line of unknown `type` or `subtype`.
