---
uid: skills-filesystem
---

# Create skills from the filesystem

Add local skills by creating `SKILL.md` files and optional supporting resources in folders that Assistant scans automatically.

You can develop skills locally without registration. Assistant scans specific project and user locations for `SKILL.md` files, loads them during static initialization, and makes valid skills available for conversations. You can start with a single skill folder and expand it with references and resources as your workflow grows.

A skill is defined by a `SKILL.md` file that contains YAML frontmatter and body instructions. The frontmatter provides the required metadata, such as the skill name and description. Optional subfolders can hold additional files that the instructions reference by relative path.

This topic explains where to [place skill files](#add-a-skill-in-a-scanned-location), how to [structure a skill folder](#organize-the-skill-folder), how to write the `SKILL.md` header, and how to use the supporting resource files.

## Prerequisites

Before you start, make sure you meet the following prerequisites:

1. Install and set up [Assistant](xref:install-assistant).
2. Have a Unity project where you can add files and folders.

## Add a skill in a scanned location

To create a local skill:

1. Choose one of the following folders that Assistant scans automatically:
   - For project-specific skills, use a folder in the `Assets` directory. `SKILL.md` files can exist anywhere in the project.
   - For skills that can be shared across projects on the same machine, use the user-specific folder:
     - On Windows: `C:\Users\<current_user>\AppData\Roaming\Unity\AIAssistantSkills` or `%APPDATA%\Unity\AIAssistantSkills`
     - On macOS: `/Users/<current_user>/Library/Application Support/Unity/AIAssistantSkills` or `~/Library/Application Support/Unity/AIAssistantSkills`

2. If you want to use the user-specific skills folder and it doesn't exist yet:

   1. Select **Edit** > **Project Settings** > **AI** > **Skills**.
   2. In **User skills**, select **Create user skills folder**.
   3. Use the newly created folder as the location for your user-specific skills.
3. In the scanned location you chose in step 1, create a folder for the skill.
4. Add a file named `SKILL.md`.
5. Add YAML frontmatter at the top of the file.
6. Add the main skill instructions under the frontmatter.
7. (Optional) Add supporting files in subfolders and reference them by relative path from `SKILL.md`.
   - `resources/` for templates and API references.
   - `references/` for detailed documentation.

A skills development folder can start with only one skill. For example, you might keep skills under `Assets/MySkills`.

### Organize the skill folder

A good convention is to name the skill folder after the skill name defined in `SKILL.md`.

**Example folder structure**

```text
Assets/MySkills
└─ my-test-skill
   ├── SKILL.md
   ├── references/
   │   ├── api-notes.md
   │   └── common-patterns.md
   └── resources/
       ├── scenario_A.md
       └── template.cs
```

Folder names, such as `resources` and `references`, are conventions only. You can use other folder names as long as the skill instructions reference the correct relative paths.

### Write the skill frontmatter

The header of `SKILL.md` uses a standard YAML frontmatter and must start and end with `---`.

The `SKILL.md` frontmatter supports the following fields:

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Unique name of the skill. It's a good convention to match the `name` field with the folder name, `my-test-skill`. |
| `description` | Yes | Describes what the skill does and when Assistant should activate it. |
| `required_packages` | Optional | A mapping of package names and versions. The skill activates only if the required packages are installed. |
| `required_editor_version` | Optional | A Unity Editor version range that determines when the skill can activate. |
| `enabled` | Optional | Set to `false` to disable the skill. The default value is `true`. |
| `tools` | Optional | A list of unique tool identifiers that Assistant can use during skill activation. |

**Example `SKILL.md` file**

Here's an example of a `SKILL.md` file:

```yaml
---
name: my-test-skill
description: Just a test skill. Activate it if we want to test skills.
required_packages:
  com.unity.inputsystem: ">=1.8.0"
  com.unity.cinemachine: ">=3.0.0, <4.0.0"
required_editor_version: ">=6000.3.13"
tools:
  - MyTools.Log
---
### Test Skill Instructions
For general activation of this skill, output "The test skill `my-test-skill` is running properly."
The user may ask about the resource in general or the C# script at this path: resources/template.cs
```

Quote version strings in YAML frontmatter, especially when they include version operators, such as `>=` or `<`, to ensure that the file parses correctly.

## Add supporting resource files

Resource files provide additional detail without increasing the size of the main skill. Because Assistant loads resources only when the instructions direct it to them, they're a good place for detailed material that might otherwise make the `SKILL.md` file too large. A practical target is to keep `SKILL.md` under about 500 lines when possible.

A skill's subfolders can contain any supporting files you need. `SKILL.md` instructions reference these files by relative path. Assistant loads these files on demand when the skill is active, so they're not part of the initial context.

They can be in any format, but common uses include:
- Code templates: A `.cs` file that Assistant uses as a starting point.
- API or domain references: An `.md` file that documents an API or workflow that Assistant consults when needed.
- Detailed step instructions: An `.md` file that contains detailed instructions for a specific step.

Examples of using the references in instructions:

- "Create a MonoBehaviour using the template at `resources/snippet.cs`."
- "For available methods and parameters, refer to `references/my-api.md`."
- "For the full placement procedure, follow `resources/placement-steps.md`."

## Understand skill loading

Assistant loads the `SKILL.md` files and their resources implicitly during static initialization, including Unity Editor startup and domain reloads.

To validate that a skill loads correctly and to reload skills on demand, refer to [Test and validate skills](xref:skills-test).

## Share skills and supporting code

After you create and test a skill, you can share it with other projects or team members. Package the skill folder and any required supporting scripts as a Unity asset package.

For example, include the following in the package:

- The skill folder that contains `SKILL.md`.
- Supporting files under `resources/` and `references/`.
- Any Editor scripts that define static utility functions or custom tools.

To export these assets as a `.unitypackage`, use Unity's asset package workflow. For more information, refer to [Create and export asset packages](https://docs.unity3d.com/Manual/AssetPackagesCreate.html).

When another user imports the package into a project, Assistant discovers the included skills from the imported files.

## Additional resources

- [Test and validate skills](xref:skills-test)
- [Use static utility functions in skills](xref:static-utility-functions)
- [Create and export asset packages](https://docs.unity3d.com/Manual/AssetPackagesCreate.html)