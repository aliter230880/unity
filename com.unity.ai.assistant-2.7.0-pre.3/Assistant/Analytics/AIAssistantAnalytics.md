# AI Assistant Analytics Events

### UI Trigger Backend Event

| SubType                        | Description                                                                                         |
|--------------------------------|-----------------------------------------------------------------------------------------------------|
| `favorite_conversation`        | Marks a conversation as favorite or not. Includes ConversationId, IsFavorite and ConversationTitle. |
| `delete_conversation`          | Deletes a previous conversation. Includes ConversationId and ConversationTitle.                     |
| `rename_conversation`          | Renames a conversation. Includes ConversationId and ConversationTitle.                              |
| `load_conversation`            | Loads a previously conversation. Includes ConversationId and ConversationTitle.                     |
| `cancel_request`               | Cancels a message request. Includes ConversationId.                                                 |
| `edit_code`                    | User edited the run command script.                                                                 |
| `create_new_conversation`      | User started a new conversation.                                                                    |
| `refresh_inspirational_prompt` | User refreshed inspirational prompt.                                                                |

---

### Context Events

| SubType                                    | Description                                                                                  |
|--------------------------------------------|----------------------------------------------------------------------------------------------|
| `expand_context`                           | User expanded the attached context section.                                                                                              |
| `expand_command_logic`                     | User expanded the command logic section.                                                                                                 |
| `ping_attached_context_object_from_flyout` | User pinged a context object from the flyout. Includes MessageId, ConversationId, ContextType and ContextContent.                        |
| `clear_all_attached_context`               | Cleared all attached context items. Includes MessageId and ConversationId.                                                               |
| `remove_single_attached_context`           | Removed a single attached context item. Includes MessageId, ConversationId, ContextType and ContextContent.                              |
| `drag_drop_attached_context`               | Dragged and dropped a context object. Includes MessageId, ConversationId, ContextType, ContextContent and IsSuccessful.                  |
| `drag_drop_image_file_attached_context`    | Dragged and dropped an image file into context. Includes MessageId, ConversationId, ContextContent (filename) and ContextType (file extension). IsSuccessful is always "true". |
| `choose_context_from_flyout`               | User chose a context object from the flyout. Includes MessageId, ConversationId, ContextType and ContextContent.                         |
| `screenshot_attached_context`              | User attached a screenshot via the screenshot button. Includes MessageId, ConversationId, ContextContent (display name) and ContextType ("Image"). |
| `annotation_attached_context`             | User attached an annotated screenshot via the annotation tool. Includes MessageId, ConversationId, ContextContent (display name) and ContextType ("Image"). Fired both when the initial screenshot is captured and when the annotated version is confirmed. |
| `upload_image_attached_context`            | User uploaded an image file via the file picker. Includes MessageId, ConversationId, ContextContent (display name) and ContextType (file extension). |
| `clipboard_image_attached_context`         | User pasted an image from the clipboard. Includes MessageId, ConversationId and ContextType ("Image"). ContextContent is null.            |

---

### Plugin Events

| SubType       | Description                                  |
|---------------|----------------------------------------------|
| `call_plugin` | User invoked a plugin. Includes PluginLabel. |

---

### UI Trigger Local Event

| SubType                                         | Description                                                                                                   |
|-------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| `open_shortcuts`                                | Opened the shortcuts panel.                                                                                   |
| `execute_run_command`                           | Ran a command from the UI. Includes MessageId, ConversationId and ResponseMessage                             |
| `use_inspirational_prompt`                      | User clicked an inspirational prompt. Includes UsedInspirationalPrompt.                                       |
| `choose_mode_from_shortcut`                     | User chose a shortcut mode. Includes ChosenMode.                                                              |
| `copy_code`                                     | User copied code from a run command response. Includes ConversationId and ResponseMessage.                    |
| `copy_response`                                 | User copied a response message from any command type. Includes ConversationId, MessageId and ResponseMessage. |
| `save_code`                                     | User saved a response message. Includes ResponseMessage.                                                      |
| `open_reference_url`                            | User clicked on a reference URL. Includes Url.                                                                |
| `modify_run_command_preview_with_object_picker` | User modified a run-command preview parameter via the object picker. Includes PreviewParameter.               |
| `modify_run_command_preview_value`              | User modified a run-command preview parameter value directly. Includes PreviewParameter.                      |
| `permission_requested`                          | A permission dialog was displayed to the user. Includes ConversationId, FunctionId and PermissionType. Fired when the dialog appears, before the user responds. |
| `permission_response`                           | User responded to a permission request. Includes ConversationId, FunctionId, UserAnswer and PermissionType.   |
| `window_closed`                                 | User closed the AI Assistant window. Includes ConversationId and IsAIRunning ("True" or "False"). |
| `window_opened`                                 | User opened the AI Assistant window. No extra fields.                                                         |
| `permission_setting_changed`                    | User changed a permission policy setting in the settings window. PermissionType contains the setting name; UserAnswer contains the new policy value. |
| `auto_run_setting_changed`                      | User toggled the Auto-Run setting. UserAnswer contains the new value ("True" or "False").                     |
| `new_chat_suggestions_shown`                    | The new-conversation screen with suggestion prompts became visible. No extra fields.                          |
| `suggestion_category_selected`                  | User clicked a suggestion category chip. Includes SuggestionCategory (e.g. "Troubleshoot", "Explore").       |
| `suggestion_prompt_selected`                    | User clicked a specific suggestion prompt. Includes SuggestionCategory and UsedInspirationalPrompt (the prompt text). |
| `mode_switched`                                 | User explicitly switched mode via the dropdown. Includes ChosenMode (e.g. "Agent", "Ask", "Plan") and ConversationId. |
| `plan_review_approved`                          | User approved the implementation plan. Includes ConversationId and ResponseMessage (plan file path).                  |
| `plan_review_feedback_sent`                     | User sent feedback on the implementation plan. Includes ConversationId, ResponseMessage (plan file path) and UserAnswer (feedback text). |
| `plan_review_cancelled`                         | User cancelled the plan review without approving or sending feedback. Includes ConversationId and ResponseMessage (plan file path). |
| `clarifying_question_submitted`                 | User submitted answers to the clarifying questions dialog. Includes ConversationId and ResponseMessage (total question count). |
| `clarifying_question_cancelled`                 | User dismissed the clarifying questions dialog without submitting. Includes ConversationId and ResponseMessage (total question count). |
| `retry_relay_connection`                        | User clicked the reconnect link after the relay connection was lost. Includes ConversationId.                 |
| `conversation_restored`                         | A conversation was restored after window reopen. Includes ConversationId and DurationMs (time to restore in milliseconds). |
| `expand_user_message_context`                   | User expanded the Context accordion on a sent user message. Includes ConversationId, MessageId and ContextItemCount. |
| `toggle_chat_history`                           | User clicked the Chats button to toggle the history panel. ActionValue is "True" (opened) or "False" (closed). |
| `scroll_to_bottom`                              | User clicked the More button to scroll to the bottom of the conversation. No extra fields.                   |
| `switch_run_command_tab`                        | User switched between Code and Output tabs in a Run Command result. Includes ConversationId and ActionValue ("Code" or "Output"). |
| `expand_function_call_result`                   | User expanded or collapsed a function call result details section. Includes ConversationId, FunctionId (tool name) and ActionValue ("True" = expanded, "False" = collapsed). |
| `open_assistant_settings`                       | User clicked "Open Assistant Settings" from the settings popup. No extra fields.                              |
| `collapse_reasoning_setting_changed`            | User toggled the "Collapse Reasoning when complete" setting. ActionValue contains the new value ("True" or "False"). ChosenMode contains the active mode ("Ask" or "Agent"). |
| `generate_content`                              | User clicked the Generate button in a generator. GeneratorType contains the generator type ("Animate", "Material", "Sound", "Mesh", "Texture", "Sprite", or "Cubemap"). Prompt contains the prompt text. NegativePrompt contains the negative prompt text. |
| `change_generator_mode`                         | User switched tabs in the Image generator. ActionValue contains the selected tab label (e.g. "Generate", "Remove BG", "Spritesheet", "Upscale", "Pixelate", "Recolor", "Inpaint"). |

---
---

## Field Schema Details

### Common Fields

| Field Name | Type   | Description                                             |
|------------|--------|---------------------------------------------------------|
| `SubType`  | string | Describes the specific type of action within the group. |

---

### UITriggerBackendEventData

| Field Name          | Type   | Description                                           |
|---------------------|--------|-------------------------------------------------------|
| `SubType`           | string | Specific subtype like 'cancel_request', etc.          |
| `ConversationId`    | string | ID of the conversation where the event occurred.      |
| `MessageId`         | string | ID of the message involved in the event.              |
| `ResponseMessage`   | string | The actual message text (if applicable).              |
| `ConversationTitle` | string | Title of the conversation.                            |
| `IsFavorite`        | string | Indicates if the conversation was marked as favorite. |

---

### ContextEventData

| Field Name       | Type   | Description                                     |
|------------------|--------|-------------------------------------------------|
| `SubType`        | string | Subtype like 'drag_drop_attached_context', etc.                                                       |
| `ContextContent` | string | Name or content of the context object. Null for `clipboard_image_attached_context` and `clear_all_attached_context`. |
| `ContextType`    | string | Type of the context object (e.g. type name, "Image", "LogData", file extension). Null for `clear_all_attached_context`. |
| `IsSuccessful`   | string | Whether the context interaction succeeded. Set for `drag_drop_attached_context` and `drag_drop_image_file_attached_context` only. |
| `MessageId`      | string | ID of the pending message this context was attached to.                                               |
| `ConversationId` | string | ID of the conversation this context was attached to.                                                  |

---

### PluginEventData

| Field Name    | Type   | Description                                 |
|---------------|--------|---------------------------------------------|
| `SubType`     | string | Subtype such as 'call_plugin', etc.         |
| `PluginLabel` | string | Identifier or label for the plugin invoked. |

---

### UITriggerLocalEventData

| Field Name                | Type   | Description                                            |
|---------------------------|--------|--------------------------------------------------------|
| `SubType`                 | string | Subtype such as 'open_shortcuts', etc.                 |
| `UsedInspirationalPrompt` | string | Inspirational prompt being used.                       |
| `SuggestionCategory`      | string | The suggestion category chip label (e.g. "Troubleshoot", "Explore"). Set for `suggestion_category_selected` and `suggestion_prompt_selected`. |
| `ChosenMode`              | string | Selected mode, such as run or ask mode.                |
| `ReferenceUrl`            | string | URL of the reference being called.                     |
| `ConversationId`          | string | ID of the conversation where the event occurred.       |
| `MessageId`               | string | ID of the message involved in the event.               |
| `ResponseMessage`         | string | Message content involved in the UI action.             |
| `PreviewParameter`        | string | Run command preview parameter after user modification. |
| `FunctionId`              | string | ID of the function/tool that requested the permission. |
| `UserAnswer`              | string | User's response or new setting value. For `permission_response`: "AllowOnce", "AllowAlways", or "DenyOnce". For `permission_setting_changed`: the new policy value. For `auto_run_setting_changed`: "True" or "False". |
| `PermissionType`          | string | For `permission_requested`/`permission_response`: category of permission (e.g. "ToolExecution", "FileSystem", "CodeExecution", etc.). For `permission_setting_changed`: the name of the setting that changed. |
| `DurationMs`              | long   | Duration in milliseconds. Set for `conversation_restored` (time to restore the conversation). |
| `ContextItemCount`        | string | Number of context items attached to the user message. Set for `expand_user_message_context`. |
| `GeneratorType`           | string | The generator type: "Animate", "Material", "Sound", "Mesh", "Texture", "Sprite", or "Cubemap". Set for `generate_content`. |
| `NegativePrompt`          | string | The user's negative prompt text. Set for `generate_content`. |
| `IsAIRunning`             | string | Whether the AI was actively processing a response ("True" or "False"). Set for `window_closed`. |
| `Prompt`         | string | The user's prompt text for content generation. Set for `generate_content`. |
| `ActionValue`             | string | The value associated with the user's action (e.g. selected tab, toggle state). Used by `toggle_chat_history`, `switch_run_command_tab`, `expand_function_call_result`, `collapse_reasoning_setting_changed`, `change_generator_mode`. |

---

### SendUserMessageEventData

| Field Name       | Type   | Description                                                                                         |
|------------------|--------|-----------------------------------------------------------------------------------------------------|
| `userPrompt`     | string | The user-entered prompt that triggered the event.                                                   |
| `commandMode`    | string | The active assistant mode when the message was sent: `"Agent"`, `"Ask"`, or `"Plan"`. Empty when mode is undefined. |
| `autoRunEnabled` | string | Whether Auto-Run is enabled. `"True"` or `"False"` when in Agent mode; null (omitted) otherwise.    |
| `conversationId` | string | Associated conversation ID.                                                                         |
| `messageId`      | string | ID of the user message being sent.                                                                  |

---

## MCP Events

### MCP Session Event (`AIAssistantMcpSessionEvent`)

Tracks MCP session lifecycle — connection, client identification, and disconnection.

| SubType              | Description                                                                                                          |
|----------------------|----------------------------------------------------------------------------------------------------------------------|
| `SessionStart`       | Fired when a client connects and completes the handshake. Includes SessionId, SimultaneousConnections, and SimultaneousDirectConnections. |
| `ClientInfoReceived` | Fired when the client sends `set_client_info`. Includes SessionId, ClientName, ClientVersion, and OldClientName.     |
| `SessionEnd`         | Fired when a client disconnects. Includes SessionId, ClientName, SessionDurationMs, TotalToolCalls, LastToolName, and TimeToFirstSuccessMs. |

#### McpSessionEventData

| Field Name           | Type   | Description                                                                            |
|----------------------|--------|----------------------------------------------------------------------------------------|
| `SubType`            | string | Session event subtype: "SessionStart", "ClientInfoReceived", or "SessionEnd".          |
| `ClientName`         | string | Client name from `set_client_info` (e.g. "claude-code", "cursor").                    |
| `ClientVersion`      | string | Client version string.                                                                 |
| `SessionId`          | string | Connection identity key for correlating events within a session.                       |
| `SessionDurationMs`  | string | Total session duration in milliseconds. SessionEnd only.                               |
| `TotalToolCalls`     | string | Number of tool calls made during the session. SessionEnd only.                         |
| `LastToolName`       | string | Name of the last tool called before disconnect. SessionEnd only.                       |
| `TimeToFirstSuccessMs` | string | Time from session start to first successful tool call in milliseconds. SessionEnd only; empty if no successful calls. |
| `OldClientName`      | string | Previous client name when the client changes identity. ClientInfoReceived only.        |
| `SimultaneousConnections` | string | Total number of MCP transport connections active at the moment this session was registered. SessionStart only. |
| `SimultaneousDirectConnections` | string | Number of direct (non-gateway) MCP transport connections at session start. SessionStart only. |

---

### MCP Tool Call Event (`AIAssistantMcpToolCallEvent`)

Tracks individual MCP tool call completions with latency and error details.

| SubType             | Description                                                                                   |
|---------------------|-----------------------------------------------------------------------------------------------|
| `ToolCallCompleted` | Fired after each tool execution. Includes SessionId, ClientName, ToolName, Status, LatencyMs. |

#### McpToolCallEventData

| Field Name     | Type   | Description                                                             |
|----------------|--------|-------------------------------------------------------------------------|
| `SubType`      | string | Always "ToolCallCompleted".                                             |
| `ClientName`   | string | Client name from `set_client_info`.                                     |
| `ToolName`     | string | The MCP tool name that was executed (e.g. "Unity_ManageScene").         |
| `Status`       | string | "Success" or "Error".                                                   |
| `ErrorType`    | string | Exception type name on failure (e.g. "ArgumentException"). Empty on success. |
| `ErrorMessage` | string | Error message on failure. Empty on success.                             |
| `LatencyMs`    | string | Execution time in milliseconds.                                         |
| `SessionId`    | string | Connection identity key to correlate with session events.               |

---

## Gateway Events

### Gateway Event (`AIAssistantGatewayEvent`)

Tracks ACP agent turn completions. Sent via EditorAnalytics (BigQuery) alongside the HTTP POST to the gateway backend.

| SubType         | Description                                                                                       |
|-----------------|---------------------------------------------------------------------------------------------------|
| `TurnCompleted` | Fired when an ACP agent turn completes. Includes conversation metadata and full message history.  |

#### GatewayEventData

| Field Name       | Type   | Description                                                                                      |
|------------------|--------|--------------------------------------------------------------------------------------------------|
| `SubType`        | string | Always "TurnCompleted".                                                                          |
| `ConversationId` | string | The agent session ID (identifies the conversation on the backend).                               |
| `Provider`       | string | The provider ID (e.g. "claude", "gemini").                                                       |
| `TurnCount`      | int    | Number of turns in this conversation so far.                                                     |
| `StartedAt`      | long   | Unix timestamp in milliseconds when the first prompt of the conversation was sent.               |
| `EndedAt`        | long   | Unix timestamp in milliseconds when this turn's response completed.                              |
| `Messages`       | string | Messages added since the last send for this conversation, as a JSON string. Typically 2 messages (user prompt + assistant response from the current turn), but may include more if intermediate turns were skipped by the 5-minute debounce. Each element has `role`, `isComplete`, `timestamp`, and `blocks` fields. The backend reconstructs the full conversation by joining all events for a `ConversationId` ordered by `EndedAt`. |

---

### Gateway Analytics HTTP Event (`/v1/assistant/ai-gateway-analytics`)

Sent via relay bus POST (subject to a 5-minute debounce). A final flush is always sent when the session ends. The payload wraps an array of events under the `ai_gateway_events` key. This is a parallel path to the EditorAnalytics event above — both fire on the same trigger.

#### GatewayAnalyticsEvent

| Field Name         | Type    | Description                                                                                 |
|--------------------|---------|---------------------------------------------------------------------------------------------|
| `conversation_id`  | string  | The agent session ID (identifies the conversation on the backend).                          |
| `provider`         | string  | The provider ID (e.g. "claude", "gemini").                                                  |
| `turn_count`       | integer | Number of turns in this conversation so far (increments each time a prompt is submitted).   |
| `started_at`       | long    | Unix timestamp in milliseconds when the first prompt of the conversation was sent.          |
| `ended_at`         | long    | Unix timestamp in milliseconds when this turn's response completed.                         |
| `messages`         | array   | Messages added since the last send for this conversation. Typically 2 messages (user prompt + assistant response from the current turn), but may include more if intermediate turns were skipped by the 5-minute debounce. Each element is a message object with `role`, `isComplete`, `timestamp`, and `blocks` fields (see `AssistantMessage.ToJson()`). The backend reconstructs the full conversation by joining all events for a `conversation_id` ordered by `ended_at`. |
