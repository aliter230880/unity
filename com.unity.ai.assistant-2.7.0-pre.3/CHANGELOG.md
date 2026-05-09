# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [2.7.0-pre.3] - 2026-05-06

### Added

- Added the Voice tooltip to Generation Data.
- Surface Unity entitlement-derived connection limits (AllowedGatewayConnections, AllowedMcpConnections) on SettingsState so UI can gate features by Personal / Pro / Enterprise tier.

### Fixed

- Fixed Assistant generating voices.
- Replace placeholder .meta file GUIDs that could collide with user projects.
- Local Unity license entitlements now contribute to MCP and AI Gateway connection limits.
- UNITY_PROJECT_PATH / --project-path targeting for the Unity MCP server now matches the project root as documented.

### Removed

- Removed the count on 3d generators and default to 1.

## [2.7.0-pre.2] - 2026-05-05

### Fixed

- Fixed Video to Motion.
- Fixed Search overflow in Assistant window.
- Fixed Convert reference model from FBX to GLB when rigging model only accepts glTF input.
- Fixed light mode references button.

### Removed

- Removed the subscription link from the trial banner.

## [2.7.0-pre.1] - 2026-04-28

### Added

- Expanded view for chat elements.
- Figma Access Token configuration in Project Settings with API verification.
- Figma-to-UI agent tools for listing screens and importing design assets from Figma projects.
- Generic CredentialSet relay channel for storing and deleting credentials in the OS keychain.
- Plan mode can now read console logs and query package info when producing an implementation plan.
- Public AssistantApi surface (Run, PromptThenRun, RunHeadless, AttachedContext, VirtualAttachment, IAgent).
- RunReadOnlyCommand tool to plan mode.

### Changed

- "Beta" chip label on the New Chat screen.
- Add more fields to gateway and MCP analytics.
- Attachment view redesign.
- Feedback redesign.
- Redesign session status banners to show context-specific messaging and actions based on user license type and points status.
- Todo progress widget header renamed from "Executing Plan" to "Task List".

### Fixed

- Async fire-and-forget tasks could block the calling thread before the first await in WithExceptionLogging.
- Attachment drag-and-drop animation.
- Autorun state now tracked in analytics for plan mode.
- Autorun toggle now enabled in plan mode.
- Changed all user-facing mentions of "points" to "credits" in Assistant window and AI button.
- ChatElementBase is no longer redundantly re-added to the root VisualElement on every SetupChatElement call.
- CheckpointEnabled (and other Assistant preferences) no longer revert to defaults after restoring a checkpoint in projects that gitignore `ProjectSettings/Packages/`.
- Conversation no longer hangs on "Preparing..." after domain reloads triggered by parallel `CodeEdit` tool calls.
- Fix What's New banner clipping behind centered footer in empty state.
- Fixed soft-lock of graph creation on large projects.
- GetFileContent now enforces a 2 MB size limit and rejects binary files, preventing editor hangs when large or binary assets are read.
- Increase "Summarizing..." timeout from 3s to 6s to reduce false positives during streaming.
- NullReferenceException in Chat Window.
- NullReferenceException in RunCommandTool when agent script uses an unauthorized namespace (System.Reflection, System.Net, etc.); agent now receives a specific error message naming the blocked namespace.
- Reasoning blocks no longer visually interrupt continuous text in chat responses.
- Removed unnecessary empty space below the text in the Details block of the Unity_RunCommand tool in the Unity MCP Project Settings section.
- RunCommand tool on Unity 6.5+.
- Show "Awaiting input..." in the progress indicator when a tool is waiting for user response.
- The **Refresh Project Overview** button is now hidden for non-Unity (ACP) providers, where it had no effect.
- Todo panel collapsed state is now preserved across domain reloads and conversation switches.
- Undo/Redo keyboard shortcuts now work in prompt text fields.
- When adding context via the `+` button, the input field shouldn't add the file name.
- `BaseInteraction.CancelInteraction()` now fires `OnCompleted` so interaction queue entries are properly cleaned up on cancel.
- `BaseInteraction<T>` and `DialogToolUiContainer` no longer throw `InvalidOperationException` on double-cancel races, preventing the UI ghost-panel state.

### Security

- Prevent API keys and tokens from leaking into relay logs and trace files.

## [2.6.0-pre.1] - 2026-04-14

### Changed

- Moved Profiler Assistant to Skills implementation.
- Update Plan mode tool descriptions.

### Fixed

- Added tooltip to conversation context counter.
- Fixed a few icons that were not dark enough in light mode.
- GetConsoleLogs tool timestamps.
- MCP Server settings subtitle text overflow.
- NullReferenceException in Chat Window.
- Preferences search bar now finds matches on the Assistant tab.
- Some image types cannot be used as assistant attachments.
- Tool renderer header icons getting squashed when tool call title is too long.

## [2.5.0-pre.2] - 2026-04-09

### Fixed

- Fixed compilation error when com.unity.ai.inference package is installed on Unity 6000.5.

## [2.5.0-pre.1] - 2026-04-09

### Added

- Grep tool.
- Plan mode.
- Project Settings `AI / Skills` page to see and validate AI Assistant skills.
- Public API "AgentTool" to declare tools for the AI Assistant.
- Skill loading logic for AI Assistant's skills loaded from the filesystem.
- Support `required_editor_version` field in SKILL.md frontmatter for editor version gating.
- Support for Unity 6000.5 Beta.
- `ask_user` tool UI with multi-question navigation, single/multi-select layout, and yes/no support.

### Fixed

- AskUser tool returns a descriptive error to the model when the questions JSON is malformed.
- Clicking a regular option after selecting Other left two radio buttons appearing selected simultaneously.
- Feedback text field left edge misaligned.
- Fixed "Ask" route get reset to "Agent" at every domain reload.
- Fixed ArgumentNullException for Upscale and PBR tabs.
- Game data collection hook not attaching when Assistant window is opened after editor startup.
- Ignore codex MCP probe connections from settings UI to avoid jitter.
- Image previews from screenshot and GetImageAssetContent tools disappearing when reasoning section collapses.
- MCP Integration in project settings.
- MCP duplicate connections for identical clients.
- MCP tool calls such as RunCommand now properly update the asset database and wait for compilation prior to running.
- NullReferenceException in Chat Window.
- PendingInlineInteractions cleared on conversation switch to prevent memory leaks.
- Permission requests now always surface immediately when the todo panel is showing.
- Relay not being copied to `~/.unity/relay` reliably for MCP use.
- Shared relay install logic between RelayService and ServerInstaller to fix Mac relay installation.
- Single Completed subscription per content object prevents redundant callbacks on recycling.
- Todo updates scoped to originating conversation ID, preventing cross-conversation bleed.
- `ask_user` Other field text misaligned by a few px compared to regular option labels.
- ripgrep code signing.

### Removed

- Unity.FindFiles tool.

## [2.4.0-pre.1] - 2026-03-30

### Added

- Add feedback source tracking for generation feedback.
- Mesh generation pivot export settings.
- USS import error surfacing in CodeEditTool for .uss files.

### Changed

- Improved CodeEdit string matching logic for line ending and indentation mismatches between LLM output and file content.

### Fixed

- Compilation when "USE_ROSLYN" is added to Scripting Define Symbols.
- Fixed clipboard attachment inverted.
- Fixed context button staying highlighted.
- Fixed duplicate Assistant window appearing when maximizing/restoring editor windows.
- NullReferenceException in Chat Window.

## [2.3.0-pre.2] - 2026-03-23

### Fixed

- Fixed What's new banner not loading correctly.

## [2.3.0-pre.1] - 2026-03-23

### Added

- Added a standalone 2D scene capture tool and enhanced the Multi-Angle Scene View tool with object focusing capabilities for better visual validation.
- Added feedback text in generators.
- Client-side `Unity.Web.Fetch` tool for hybrid web search architecture with HTML-to-markdown conversion, Discourse JSON API support, and browser User-Agent for bot-detection bypass.
- Game-Data-collection package: README with onboarding guide for the game-data-collection package.
- The feature of GetDependency tool to query the dependency graph.
- Toggle in project settings for batch mode auto-approve for Unity MCP.

### Changed

- Game-Data-collection package: Snapshot commit messages now include the first 15 words of conversation content for easier identification.
- MCP connection speed to be much faster.

### Fixed

- Assistant tries and fails repeatedly to generate a sprite with a background color.
- Fixed banner not dismissing after package update from package manager.
- Invalid model selected in generator windows.
- NullReferenceException in Chat Window.
- Assistant issues related to editor not in focus.
- Brittle connection behaviour due to multiple tool updates during MCP server initialization phase.
- Connection issues related to editor not in focus.
- MCP connection issues in batch mode.
- MCP reconnection after domain reload.
- MCP tool calls failing during domain reload validation window.

## [2.2.0-pre.1] - 2026-03-16

### Added

- Added feedback system for Generators.
- LLM Model selection for AI Assistant.
- Added dependency graph creation and refreshing.

### Changed

- Changed feedback UI for Assistant.
- Moved Profiler Assistant to Skills implementation.

### Fixed

- Clear all attachments (objects, console logs, virtual attachments) after sending a prompt, not just screenshots.
- Cursor access fixed.
- NullReferenceException in Chat Window.
- Project image asset contents unable to be read by AI Assistant.

## [2.1.0-pre.1] - 2026-03-09

### Added

- Tool Unity.GetSceneInfo.

### Changed

- `GameObjectSerializer`, `ObjectsHelpers`  `Response` in `Unity.AI.MCP.Editor.Helpers` to `public`.
- Changed how annotations are sent to backend by sending a mask of the annotations.
- Removed SceneInfo, ProjectDependency, ProjectSetting, ProjectVersion from GetProjectData tool output.

### Fixed

- NullReferenceException in Chat Window.

## [2.0.0-pre.1] - 2026-03-03

### Added

- Made MCP tools and APIs public, including UI for MCP tool categories and documentation.
- Added Skills Developer Frontend.
- Added opacity slider annotation.
- Added logging for code edit issues.

### Changed

- Merged `com.unity.ai.toolkit` and `com.unity.ai.generators` packages into `com.unity.ai.assistant`.
- Reduced monopackage public API.
- Updated Assistant interface, integrations, and skill creation documentation.
- Added ability to dismiss low points banner.
- Added tags to identify SkillDefinition for removal in Skills Frontend.
- Removed icons from the provider selector.
- Improved log debugger.
- Used full install path to reference Cursor CLI for running login.

### Fixed

- Fixed Annotation Tool taking screenshot on same monitor as Unity on macOS.
- Fixed 99% stuck generations.
- Fixed asset generator thought element reuse.
- Fixed getting stuck after an answer.
- Fixed domain reload endless blocker.
- Fixed ACP client stuttering.
- Fixed MCP Client initialization bug.
- Fixed a regression with relay.
- Fixed missing generation result checks.
- Fixed cancellation issues where prompt cancellation also canceled title generation.
- Labeled canceled messages as complete.
- Fixed skill definition metadata and YAML parsing corrections.
- Fixed thought sequence hiding if tool is still in progress.
- Ensured thought chunks are rendered as thought blocks for ACP conversations.
- Fixed various asset generator bugs.
- Fixed issue with stale queue indices when switching conversations.

## [1.7.0-pre.1] - 2026-02-23

### Added

- Annotation Tool

### Fixed

- Fixed interrupted conversations in AI Gateway
- Fixed RunCommand failing
- Fixed permission issues from UI refactor
- Improved conversation recovery and resilience

## [1.6.0-pre.3] - 2026-02-16

### Added

- Added Checkpoints
- Added MCP Client

### Fixed

- Fixed Mac Relay Code signing

## [1.6.0-pre.2] - 2026-02-10

### Added

- Re-enabled Unity 6.0 support

### Changed

- Removed AI Gateway from settings pages

### Fixed

- Fixed reasoning collapse issue
- Fixed race condition in DispatchWithOverride causing truncated chat responses

## [1.6.0-pre.1] - 2026-02-09

### Added
- Added links to the Profiler Window for the profiling agent integration

## [1.5.0-pre.2] - 2026-01-15

### Added

- What's New section about points.

### Changed

- Changed package description.

### Fixed

- Fixed right-click issue in chat history.
- Fixed new missing permissions cost.

## [1.5.0-pre.1] - 2026-01-05
- Added image and screenshot attachments support with multiple format compatibility
- UI Builder Agent with a preview system
- Permissions system for file create/modify/delete actions with session overrides
- New Ask mode for question-focused interactions without executing actions
- Code Edit tools with diff and syntax highlighting
- Package Manager tools for add/remove/embed packages
- Project Overview tool for quick project structure summary
- New Orchestrator with improved multi-step workflows and automatic tool review
- Added reasoning blocks with auto-hide options

## [1.0.0-pre.12] - 2025-08-12
- Update AI-Toolkit to 1.0.0-pre.18

## [1.0.0-pre.11] - 2025-07-31
- Added tooltips to actions buttons
- Cannot press enter anymore to cancel chat progress
- Fuzzy matching improvements
- Added links to new docs
- Conversation is retained on domain reload
- Remove preview chips from routes

## [1.0.0-pre.10] - 2025-07-16
- Minor bug fixes
- Fixed issue where URLs were not being updated automatically for upgrading users
- Fixed issue where versions API endpoint reporting unsupported server versions was not shutting down the UI

## [1.0.0-pre.9] - 2025-07-15
- Return enter cancels prompt and resends it
- Fix code edit temp file
- Check for unauthorized code before loading the assembly
- Support function calling during streaming

## [1.0.0-pre.8] - 2025-06-27
- Moved Plugin Attributes back to AI Assistant

## [1.0.0-pre.7] - 2025-06-25
- Minor bug fixes
- Improved error handling for function calling
- Finalized onboarding content
- Disabled run when code won't compile
- Fixed performance issues for long conversations
- Improved history panel performance

## [1.0.0-pre.6] - 2025-06-05
- Bump AI Toolkit to 1.0.0-pre.12
- Fixed issue blocking .NET Standard Editor compatibility
- Minor UI fixes
- Minor bug fixes
- Improved attachment selection UX
- Fixed issue adding additional attached context to prompts
- Added support for array types as run parameter field.

## [1.0.0-pre.5] - 2025-05-14
- Improved Agent deletion logic
- Improved search window performance
- Improved automatic conversation naming
- Fixed issue with conversation history after editing agent actions
- Other small bug fixes

## [1.0.0-pre.4] - 2025-05-13
- Update AI-Toolkit to 1.0.0.-pre.7
- Added console tab attachments
- Updated to beta backend urls
- Fixed source attribution placement
- General usability fixes
- Fixed access token refresh issue
- Fixed bugs related to API alignment
- Assistant can now be used when the editor is paused

## [1.0.0-pre.3] - 2025-04-22

### Changed
- update AI-Toolkit to 1.0.0.-pre.4

## [1.0.0-pre.2] - 2025-04-16

### Changed
- Version Bump for re-release in production

## [1.0.0-pre.1] - 2025-04-11

### Changed
- Initial release of the AI Assistant Package
- Adds a menu item at `Window > AI > Assistant` to access tool
- Updated to interact with the new AI Assistant server using the AI Assistant protocol
