# Using Skills

Skills provide a standardized way to equip ADK agents with new capabilities by wrapping instructions, documentation (references), resources (assets), and executable scripts into a single packaged unit. The C# ADK includes a full implementation matching the Python ADK.

## Concepts

A `Skill` is a self-contained capability mapped from a folder containing:
- **`SKILL.md` (Frontmatter and Instructions):** Defines the name, description, and the core markdown prompt on how to use the skill.
- **Resources (`References`):** Markdown docs or text files guiding the model.
- **Resources (`Assets`):** Code templates, schemas, data files, or text.
- **Resources (`Scripts`):** Executable bash/python code the agent can run.

## Defining a Skill in C#

You define a skill by instantiating the `Skill` model and its `Frontmatter` and `Resources`.

```csharp
using GoogleAdk.Core.Skills;
using GoogleAdk.Core.Tools;

var skill = new Skill
{
    Frontmatter = new Frontmatter
    {
        Name = "hello-world-skill",
        Description = "A skill to greet the world."
    },
    Instructions = "When asked to greet, you MUST use `load_skill_resource` to read `assets/greetings.json` first."
};

// Add resources (simulating files in a directory)
skill.Resources.Assets["greetings.json"] = "[\"Hello World\", \"Hola Mundo\"]";
skill.Resources.Scripts["greet.sh"] = new Script { Src = "echo 'Greeting executed'" };
```

## Loading skills from disk (`SkillLoader`)

For production or samples, you usually load skills from a folder on disk instead of building `Skill` objects by hand. Use `GoogleAdk.Core.Skills.SkillLoader.LoadFromDirectory`.

### Folder layout

Point `LoadFromDirectory` at a directory that contains:

| Path | Required | Purpose |
|------|----------|---------|
| `SKILL.md` | Yes | YAML frontmatter between `---` delimiters, then markdown body (L2 instructions). |
| `references/` | No | Extra docs; files become keys like `doc.md` (relative path under this folder). |
| `assets/` | No | Templates, JSON, images, etc.; same relative-key rules. |
| `scripts/` | No | Script source; each file becomes a `Script` entry keyed by relative path. |

Example `SKILL.md`:

```markdown
---
name: my-skill
description: Short line the model uses to decide when to load this skill.
---
Full instructions for the model go here after the closing `---`.
```

Frontmatter is parsed with YamlDotNet. Supported keys today include `name`, `description`, `license`, `compatibility`, `allowed-tools`, and a nested `metadata` map (when YamlDotNet deserializes it as a dictionary).

Text files under `references/` and `assets/` are read as UTF-8 strings. A small set of extensions (for example `.png`, `.jpg`, `.pdf`, `.zip`) are loaded as `byte[]` for binary handling in tools.

### Single skill from a path

```csharp
using GoogleAdk.Core.Skills;
using GoogleAdk.Core.Tools;

var skill = SkillLoader.LoadFromDirectory("/path/to/my-skill");
var toolset = new SkillToolset(new[] { skill });
```

The path can be absolute or relative to the process current directory. At runtime, console apps often run from `bin/Debug/net10.0/`, so resolve the skill root explicitly (for example walk parent directories from `AppContext.BaseDirectory`, or copy the skill folder into output with MSBuild).

### Multiple skills (dynamic catalog)

`SkillLoader` loads one skill per directory. To expose several skills, load each folder and pass the list to one toolset:

```csharp
var skillsDir = "/path/to/skills"; // contains subfolders, each with its own SKILL.md
var skills = Directory
    .GetDirectories(skillsDir)
    .Select(SkillLoader.LoadFromDirectory)
    .ToList();

var toolset = new SkillToolset(skills);
```

Duplicate `Frontmatter.Name` values are rejected by `SkillToolset` at construction time.

### Sample: `.skill` next to the project

The **`samples/GoogleAdk.Samples.Skills`** sample ships a `.skill/` folder beside the project. At startup it walks parent directories starting from `AppDomain.CurrentDomain.BaseDirectory` until it finds a directory named `.skill`, then calls `SkillLoader.LoadFromDirectory`. That way the skill still resolves when the process runs from `bin/Debug/net10.0/` or from the solution root.

## Creating the Toolset

The `SkillToolset` handles all LLM prompting and tool registration required for your agent to discover and interact with the skill.

```csharp
// Pass your skills to the toolset. 
// Optional: provide a BaseCodeExecutor if you want your agent to execute scripts via `run_skill_script`.
var toolset = new SkillToolset(new[] { skill }, optionalCodeExecutor);

// Add to your agent
var agent = new LlmAgent(new LlmAgentConfig
{
    Name = "SkillAgent",
    Model = "gemini-2.5-flash",
    Instruction = "You are an assistant.",
    Tools = [toolset]
});
```

## How It Works

When `SkillToolset` is added to an agent:
1. It automatically appends systemic guidance telling the LLM about available skills.
2. It registers 4 foundational tools:
   - `list_skills`
   - `load_skill` (to read `Instructions`)
   - `load_skill_resource` (to inspect files)
   - `run_skill_script` (to execute scripts via the code executor)
3. The LLM interacts autonomously to load instructions, read files, and execute code as defined by your custom skill.