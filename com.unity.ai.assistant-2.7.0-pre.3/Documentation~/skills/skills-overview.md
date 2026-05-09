---
uid: skills-overview
---

# About skills

Use skills to extend Assistant with reusable workflows.

Skills are modular capabilities that extend Assistant with instructions and metadata for specific workflows. They reduce repeated prompting and support domain-specific tasks without hard-coded logic. They also keep Assistant focused with use of progressive disclosure, initially loading only skill metadata.

Skills can also work together in a conversation. Assistant combines multiple skills for more complex tasks in the Unity Editor.

A skill can rely on built-in Assistant actions, such as reading project files and assets, querying scene objects and component data, capturing screenshots, and running or editing C# code. These actions are always available to any skill without additional declaration.

A practical way to develop a skill is to first try the workflow directly in Assistant chat. When you find a workflow that works well, capture it concisely in a `SKILL.md` file so that Assistant can reproduce it reliably without repeated prompts. Skill instructions don't need to follow a fixed syntax. Write them in natural language, such as "take a screenshot to verify a result", "check whether an object has a component", or "invoke a C# script to reposition an object".

For information on how to create and test skills, refer to [Create skills from the filesystem](xref:skills-filesystem) and [Test and validate skills](xref:skills-test).

## Characteristics of a skill

Skills have the following characteristics:

- **Modular instructions and metadata**: Skills package instructions and metadata that teach Assistant a workflow or domain-specific task.
- **Progressive disclosure**: Only skill metadata is always loaded in the context window. Supporting instructions and referenced files are loaded when needed.
- **Reusable natural-language workflows**: Skills describe workflows in concise natural language and don't require a strict phrasing pattern.
- **Always-available built-in actions**: Skills can guide Assistant to use built-in Editor actions, such as reading project files, querying scene data, capturing screenshots, and running or editing C# code.
- **Support files loaded on demand**: Skills can reference files by relative path, and Assistant loads those files only when the skill instructions direct it to.
- **Support for project-specific APIs**: Skills can direct Assistant to call static utility functions by a fully qualified name through C# code execution.

## Use supporting files to keep skills focused

A skill can include supporting files in subfolders and reference them by relative path from `SKILL.md`. These files are loaded on demand when the skill is active rather than being included in the initial context.

Supporting files can contain code templates, API references, or detailed step-by-step instructions. This keeps the main `SKILL.md` file concise while still allowing the skill to access detailed information when required.

## Extend skills with static utility functions

Besides built-in Assistant actions, a skill can direct Assistant to call static utility functions by invoking C# code. Static utility functions are public static C# methods that wrap project-specific Editor workflows or APIs. Use them when an operation is too specific or too fragile for open-ended code generation.

A skill calls a static utility function by its fully qualified name and can reference an API file under `resources/` for parameter and return-value details. Static utility functions don't need to be declared in `SKILL.md` frontmatter. The examples in this documentation are illustrative only, and skill instructions don't need to follow a fixed syntax.

## Extend skills with custom tools

Besides built-in actions and static utility functions, a skill can use [custom tools](xref:custom-tools) to perform structured operations.

A custom tool is a public static C# method annotated with the `[AgentTool]` attribute. Assistant discovers these tools automatically and uses the provided descriptions to determine when to call them and what values to pass.

Unlike static utility functions, which the skill calls through generated C# code, tools are invoked directly by Assistant based on their definitions and parameter descriptions.

Use custom tools when you want to expose higher-level or reusable operations, such as creating assets, querying project data, or performing file operations.

For information on how to define and configure tools, refer to [Create custom tools](xref:custom-tools).

## Additional resources

- [Create skills from the filesystem](xref:skills-filesystem)
- [Use static utility functions in skills](xref:static-utility-functions)
- [Create custom tools](xref:custom-tools)