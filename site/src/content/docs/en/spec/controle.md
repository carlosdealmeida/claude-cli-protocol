---
title: Control
description: The control requests in both directions — from the host to claude and from claude to the host —, the permission request, the answer and its rule.
---

A control request is a `control_request` line with a `request_id`, and the answer is a
`control_response` line that repeats that id. Requests go both ways:

- **from the host to `claude`**: `initialize`, interrupt, the change of mode, model and effort, the
  queries;
- **from `claude` to the host**: the permission request (`can_use_tool`) and the hook, MCP and
  elicitation requests, which this protocol does not serve.

## The shape

The request:

<!-- stdin -->
```json
{"type":"control_request","request_id":"mode-0","request":{"subtype":"set_permission_mode","mode":"acceptEdits"}}
```

The success answer, with the body in `response.response` (when there is a body):

<!-- fixture: mode-and-model.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "mode-0", "response": {"mode": "acceptEdits"}}}
```

The error answer, with the text in `response.error`:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

- **The `request_id` belongs to whoever asks.** The host picks the id of its requests; `claude`
  picks the id of its own. The library makes a new id per request (a 32-digit GUID, without
  hyphens): the caller does not choose it. The recorder uses readable ids (`init-1`, `mode-0`), and
  that is why they show up in the recordings.
- **The answer does not say which request it answers, only the id.** Whoever asked keeps the
  request by its id, to know what the answer confirms. In the library, `ClaudeSession` does this
  and hands out the pair as `control_replied`.
- **The order is not guaranteed.** An answer can arrive in the middle of the lines of a turn.

## From the host to claude

| `subtype` | What it asks | Request body | Answer body | Recorded |
|---|---|---|---|---|
| `initialize` | The handshake; the official SDKs send it first | — | `commands`, `models`, `account`, `auth_conflict` | all |
| `interrupt` | Stops the turn | — | `still_queued`: the messages still waiting their turn | `interrupted` |
| `set_permission_mode` | Changes the mode | `mode` | `mode` | `mode-and-model`, `plan*` |
| `set_model` | Changes the model | `model` | `model`, already resolved | `mode-and-model`, `effort` |
| `apply_flag_settings` | Changes the effort | `settings.effortLevel` (text or `null`) | none | `effort` |
| `get_settings` | The settings in force | — | `applied` (`model`, `effort`) | `effort` |
| `get_context_usage` | The use of the context window | — | `categories`, `totalTokens`, `maxTokens`, `percentage` | `commands` |
| `get_task_output` | What a background task has printed | `task_id` | — | no |
| `stop_task` | Stops a background task | `task_id` | none | `stop-task` |
| `remote_control` | Turns Remote Control on or off | `enabled` | `session_url`, when turning on | no |
| `get_chrome_dialog` | The state of the browser extension | — | — | no |

### `initialize`

<!-- stdin -->
```json
{"type":"control_request","request_id":"init-1","request":{"subtype":"initialize"}}
```

The answer lists the slash commands of the session, the models and the account:

<!-- fixture: edit.stream.ndjson:1 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "init-1", "response": {"commands": [{"name": "compact", "description": "Free up context by summarizing the conversation so far", "argumentHint": "<optional custom summarization instructions>", "builtin": true}, "…"]}}}
```

- `commands[]`: `name` (without the slash), `description`, `argumentHint`.
- `models[]`: `value`, `resolvedModel`, `displayName`, `description` and `supportedEffortLevels`.
- `account`: the login (`apiProvider`, `tokenSource`, `apiKeySource`), `email`, `organization` and
  `subscriptionType`. The recordings do not have it: the recorder drops it, because it is the
  account of whoever recorded.
- `auth_conflict`: when there is a token and an API key at the same time, the names of the two
  sources (`token_source` and `api_key_source`), never the credentials.

Every recording starts with the answer to `initialize`, on line 1, because the recorder sends it
before any message. The `system/init` line only comes with the first message.

### `interrupt`

<!-- fixture: interrupted.stream.ndjson:22 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "interrupt-1", "response": {"still_queued": []}}}
```

The interrupted turn ends with a `result` of `error_during_execution`. A permission request that was
open is cancelled first (see [`control_cancel_request`](#control_cancel_request)).

### `set_permission_mode` and `set_model`

The mode or the model hold from the success answer on. The answer to `set_model` carries the
resolved model: in the `effort` scenario, the request for `haiku` comes back as
`claude-haiku-5-5`.

<!-- fixture: effort.stream.ndjson:14 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "model-3", "response": {"model": "claude-haiku-5-5"}}}
```

The modes the library accepts asking for are `plan`, `default`, `acceptEdits` and `auto`. A mode
outside that list is not written (see [Transport](../transporte/#the-check)).

### `apply_flag_settings` and `get_settings`

<!-- stdin -->
```json
{"type":"control_request","request_id":"effort-1","request":{"subtype":"apply_flag_settings","settings":{"effortLevel":"high"}}}
```

The answer confirms nothing: it is a success with no body. The effort in force comes in the answer
to `get_settings`:

<!-- fixture: effort.stream.ndjson:2 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "effort-1"}}
```

<!-- fixture: effort.stream.ndjson:3 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "settings-1", "response": {"applied": {"model": "claude-haiku-5-5", "effort": "high"}}}}
```

`effortLevel: null` goes back to the model's default effort. Since 2.1.325, an invalid value is
refused with an error answer.

### `get_context_usage`

<!-- fixture: commands.stream.ndjson:12 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "context-2", "response": {"categories": [{"name": "System prompt", "tokens": 3129, "kind": "used"}, "…"], "totalTokens": 25341, "maxTokens": 1000000, "percentage": 3}}}
```

`kind` is `used`, `deferred`, `buffer` or `free`. When present, `model`, `memoryFiles` and `agents`
complete the answer.

### `stop_task`

<!-- stdin -->
```json
{"type":"control_request","request_id":"stop-1","request":{"subtype":"stop_task","task_id":"bn9i1f619"}}
```

The `task_id` is the one of the `system/background_tasks_changed` or `system/task_started` line.
`claude` ends the task and only then replies. In the `stop-task` scenario, the `task_notification`
with `status: "stopped"` arrives at line 30, the empty list of tasks at line 31 and the reply at
line 32:

<!-- fixture: stop-task.stream.ndjson:32 -->
```json
{"type": "control_response", "response": {"subtype": "success", "request_id": "stop-1", "response": {}}}
```

### How the body is read

The library does not know which request an answer answers: it reads the body by its shape.

| If the body has | It reads |
|---|---|
| `commands` | the slash commands; and, if it also has `models` or `account`, the `auth_conflict` |
| `models` | the models |
| `applied` | the settings in force |
| `categories` and `totalTokens` | the use of the context |
| `account` | the account |
| `rate_limits` | the usage limits (the answer to a `get_usage`, which the library does not ask for but reads) |
| `session_url` | the Remote Control link |

## From claude to the host

### `can_use_tool`, the permission request

With `--permission-prompt-tool stdio`, each tool that needs permission becomes a request on stdout,
and `claude` waits for the answer on stdin:

<!-- fixture: edit.stream.ndjson:40 -->
```json
{"type": "control_request", "request_id": "0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e", "request": {"subtype": "can_use_tool", "tool_name": "Edit", "display_name": "Edit", "input": {"file_path": "C:\\demo\\app\\notes.md", "old_string": "line three\n", "new_string": "line 3 (edited)\n", "replace_all": false}, "description": "notes.md", "permission_suggestions": [{"type": "setMode", "mode": "acceptEdits", "destination": "session"}], "tool_use_id": "toolu_01FPyYmYbFzFZR24ZJtHzcWB"}}
```

| Field | What it is |
|---|---|
| `tool_name` | The tool. Without it, the library yields no event, and the transport answers with an error |
| `display_name`, `description` | How to show the request |
| `input` | What the tool is going to do, as the model asked |
| `permission_suggestions` | The rules `claude` suggests keeping so as not to ask again |
| `tool_use_id` | The tool call of the request |
| `agent_id` | The sub-agent that asks (its `task_id`); absent for the main conversation |
| `decision_reason`, `decision_reason_type`, `decision_reason_code` | Why `claude` asks: the text of a hook, of a safety check… The type is `hook`, `safetyCheck`, `subcommandResults`, `other`… |
| `default_to_no` | `claude` asks for "no" to be selected |
| `suppress_always_allow_rule` | `claude` offers no rule to keep: no "allow always" |
| `mcp_server.source` | Where the MCP server of the tool comes from; `claudeai` for a claude.ai connector |

The recordings have requests for `Edit`, `Write`, `Bash`, `PowerShell`, `ExitPlanMode` and
`AskUserQuestion`. The one from a sub-agent has `agent_id` (`subagent.stream.ndjson`, line 50); the
one from a hook, `decision_reason_type: "hook"` (`hook-ask`, line 20); the one for a protected
folder, `decision_reason_type: "safetyCheck"` (`seguranca`, line 20).

### The permission answer

Three decisions, in a `control_response` of `success` with the `request_id` of the request:

**Allow**: the `input` goes back as it came, in `updatedInput`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"0f8155c2-5135-4e0b-9da0-4cbc9d41ee1e","response":{"behavior":"allow","updatedInput":{"file_path":"C:\\demo\\app\\notes.md","old_string":"line three\n","new_string":"line 3 (edited)\n","replace_all":false}}}}
```

**Allow always**: the same, plus the `permission_suggestions` of the request in
`updatedPermissions`, when they are a list.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"allow","updatedInput":{"…":"…"},"updatedPermissions":[{"type":"setMode","mode":"acceptEdits","destination":"session"}]}}}
```

**Deny**: with the message for the model. Without a message, the library sends
`"The user denied this tool use."`.

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"success","request_id":"…","response":{"behavior":"deny","message":"Do not create files, just explain."}}}
```

The message reaches the model as the text of a `tool_result` with `is_error: true`. In the `denied`
scenario, the model reads the message and explains in the `result` that it did not create the file.

:::note[Leaving plan mode]
The end of a plan is a permission request for `ExitPlanMode`. To leave plan mode and go on with the
plan, the recorder first sends a `set_permission_mode` for the next mode (`acceptEdits` in the
`plan` scenario, `auto` in `plan-auto`) and then allows. Denying with a message gives the plan back
to the model with that message (`plan-feedback` scenario).
:::

### The permission answer rule

> The answer to a permission request always carries the `input` and the `permission_suggestions`
> that the process side itself received from `claude`. The caller only chooses the `request_id`,
> the decision and the message.

It is a rule of the library, not of `claude`: it accepts whatever `updatedInput` it receives, and
that is what the field is for. But whoever holds the stdin of a `claude` should not run an `input`
that came from somewhere else. If the request goes through a UI, a network or another process,
whoever answers could swap `"command": "ls"` for another command, and `claude` would run the swapped
command with the permission given to the first one.

In the library:

- **the answer command (`AnswerPermission`) has no field for the `input`** nor for the
  suggestions: only the id, the decision and the message;
- **whoever writes to stdin builds the answer from the request it read itself** from stdout, kept in
  a ledger of requests (`PermissionLedger`);
- **each request is answered once.** Answering takes the request out of the ledger. It also leaves
  when `claude` cancels it, when the `tool_result` of its call arrives and when the turn ends. An id
  that is not pending (already answered, cancelled, resolved or unknown) writes nothing.

Since 2.1.321, `AskUserQuestion` denies the answer of a host that touches the `input` (field
`afkTimeoutMs`). With the rule, that does not happen.

:::caution[Not checked yet]
With `--permission-prompt-tool`, a large "allow" result is written to a file by MCP's large-output
handling ([#109249](https://github.com/anthropics/claude-code/issues/109249)). The answer sends the
whole `input` back, so a `Write` of a large file goes through there. It still has to be seen with a
real `claude`.
:::

### `control_cancel_request`

`claude` gives up on a permission request that has had no answer yet:

<!-- fixture: interrupted.stream.ndjson:21 -->
```json
{"type": "control_cancel_request", "request_id": "dbfbeade-6899-46b2-ba43-120f8dea7acd"}
```

In the `interrupted` scenario, the `PowerShell` request on line 20 is cancelled on line 21, by the
interrupt. After the cancellation, the request leaves the ledger, and an answer to it writes
nothing.

### The request nobody serves

`claude` can also send control requests of other kinds: the callback of an SDK hook, a message for
an SDK MCP server, an elicitation. Without an answer, it waits forever. The library's transport
answers any of them with an error:

<!-- stdin -->
```json
{"type":"control_response","response":{"subtype":"error","request_id":"…","error":"not supported"}}
```

And a `can_use_tool` the reader cannot read (without `tool_name`, for instance), which therefore
would never reach whoever answers, gets the error `"malformed permission request"`.
