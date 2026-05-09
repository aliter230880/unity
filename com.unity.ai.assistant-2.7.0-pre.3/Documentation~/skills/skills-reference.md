---
uid: skills-reference
---

# Manage Skills page reference

Explore the settings in the **Manage Skills** page to review discovered skills, scanned skill locations, reported issues, and skill status.

https://internal.docs.unity.com/en-us/docs-quality/documentation/content-model/um-ui-reference-pages-structure-inspectors-properties-settings

You can open this page from the Unity Editor through **Edit** > **Project Settings** > **AI** > **Skills**.

## Manage Skills settings

The **Manage Skills** page contains the following settings and UI elements:

| **Setting** | **Description** |
| ----------- | --------------- |
| **Rescan** | Rescans the configured skill locations and refreshes the list of discovered skills. |
| **Last scanned** | Shows when Assistant last scanned for skills. |
| **Filter skills by name** | Filters the displayed skill entries by name. |
| **Skills with issues detected** | Lists skills that contain parsing or validation issues. Expand an entry to review the issue and file location. |
| **Project skills** | Lists skills discovered in the current project's scanned locations. |
| **User skills** | Lists skills discovered in the user-level scanned location. |
| **Skills location** | Shows the folder path that Assistant scans for skills in the current section. |
| Skill entry | Represents a discovered skill. Expand the entry to inspect details or issues. |
| **Disabled** | Indicates that a discovered skill is disabled. |

### Skills with issues detected

The **Skills with issues detected** section contains the following details:

| **Setting** | **Description** |
| ----------- | --------------- |
| Issue entry | Shows the affected skill name. |
| Issue message | Describes the detected problem, such as a missing required YAML frontmatter field. |
| **File location** | Shows the file system path of the affected skill so you can locate and correct it. |
| Open location button | Opens the file location for the selected skill entry. |

### Project skills

The **Project skills** section contains the following details:

| **Setting** | **Description** |
| ----------- | --------------- |
| **Skills location** | Shows the project-level folder path that Assistant scans for skills, such as `Assets`. |
| Skill entry | Lists each discovered project skill. Expand the entry to inspect the skill. |

### User skills

The **User skills** section contains the following details:

| **Setting** | **Description** |
| ----------- | --------------- |
| **Create user skills folder** | Creates the user-level skills folder if it doesn't already exist. After you create the folder, you can add user skills to it and Assistant scans that location for skills. |
| **Skills location** | Shows the user-level folder path that Assistant scans for skills. |
| Skill entry | Lists each discovered user skill. Expand the entry to inspect the skill. |

## Formatting and validation issues

The **Manage Skills** page reports issues when Assistant detects invalid or incomplete skill definitions.

When a skill appears in **Skills with issues detected**, expand the skill entry to review the issue message and file location, then correct the skill file and select **Rescan** to refresh the list.

Each skill must have a unique name value in its `SKILL.md` frontmatter. If multiple skills use the same name, Assistant registers only one skill and reports the duplicate as an issue. To avoid conflicts, ensure that every skill uses a unique name, even when skills are stored in different locations, such as project skills and user skills.

## Additional resources

- [Create skills from the filesystem](xref:skills-filesystem)
- [Test and validate skills](xref:skills-test)