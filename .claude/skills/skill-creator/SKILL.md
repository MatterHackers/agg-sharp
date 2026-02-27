---
name: skill-creator
description: "Guide for creating effective skills. This skill should be used when users want to create a new skill (or update an existing skill) that extends Claude's capabilities with specialized knowledge, workflows, or tool integrations."
---

# Skill Creator

This skill provides guidance for creating effective skills.

## About Skills

Skills are modular, self-contained packages that extend Claude's capabilities by providing
specialized knowledge, workflows, and tools. Think of them as "onboarding guides" for specific
domains or tasks—they transform Claude from a general-purpose agent into a specialized agent
equipped with procedural knowledge that no model can fully possess.

### What Skills Provide

1. Specialized workflows - Multi-step procedures for specific domains
2. Tool integrations - Instructions for working with specific file formats or APIs
3. Domain expertise - Company-specific knowledge, schemas, business logic
4. Bundled resources - Scripts, references, and assets for complex and repetitive tasks

### Anatomy of a Skill

Every skill consists of a required SKILL.md file and optional bundled resources:

```
skill-name/
├── SKILL.md (required)
│   ├── YAML frontmatter metadata (required)
│   │   ├── name: (required)
│   │   └── description: (required)
│   └── Markdown instructions (required)
└── Bundled Resources (optional)
    ├── scripts/          - Executable code
    ├── references/       - Documentation intended to be loaded into context as needed
    └── assets/           - Files used in output (templates, icons, fonts, etc.)
```

#### SKILL.md (required)

**Metadata Quality:** The `name` and `description` in YAML frontmatter determine when Claude will use the skill. Be specific about what the skill does and when to use it. Use the third-person (e.g. "This skill should be used when..." instead of "Use this skill when...").

#### Bundled Resources (optional)

##### Scripts (`scripts/`)

Executable code for tasks that require deterministic reliability or are repeatedly rewritten.

- **When to include**: When the same code is being rewritten repeatedly or deterministic reliability is needed
- **Benefits**: Token efficient, deterministic, may be executed without loading into context

##### References (`references/`)

Documentation and reference material intended to be loaded as needed into context to inform Claude's process and thinking.

- **When to include**: For documentation that Claude should reference while working
- **Use cases**: API documentation, domain knowledge, company policies, detailed workflow guides
- **Benefits**: Keeps SKILL.md lean, loaded only when Claude determines it's needed
- **Best practice**: If files are large (>10k words), include grep search patterns in SKILL.md

##### Assets (`assets/`)

Files not intended to be loaded into context, but rather used within the output Claude produces.

- **When to include**: When the skill needs files that will be used in the final output
- **Use cases**: Templates, images, icons, boilerplate code, fonts, sample documents
- **Benefits**: Separates output resources from documentation

## Skill Creation Process

### Step 1: Understanding the Skill with Concrete Examples

To create an effective skill, clearly understand concrete examples of how the skill will be used. Ask questions like:
- "What functionality should this skill support?"
- "Can you give some examples of how this skill would be used?"
- "What would a user say that should trigger this skill?"

### Step 2: Planning the Reusable Skill Contents

Analyze each example to identify what scripts, references, and assets would be helpful when executing these workflows repeatedly.

### Step 3: Create the Skill Directory

Create the skill directory with the required structure:
```
.claude/skills/skill-name/
├── SKILL.md
├── scripts/     (optional)
├── references/  (optional)
└── assets/      (optional)
```

### Step 4: Edit the Skill

**Writing Style:** Write the entire skill using **imperative/infinitive form** (verb-first instructions). Use objective, instructional language.

To complete SKILL.md, answer:
1. What is the purpose of the skill, in a few sentences?
2. When should the skill be used?
3. In practice, how should Claude use the skill?

### Step 5: Iterate

After testing the skill, improve based on real usage:
1. Use the skill on real tasks
2. Notice struggles or inefficiencies
3. Identify how SKILL.md or bundled resources should be updated
4. Implement changes and test again
