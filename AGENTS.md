# Project Instructions

This repository may be worked on by different AI agents,
models, sessions, accounts, and computers.

Git and committed repository files are the authoritative project state.
Do not rely on previous conversation memory.

## Before starting work

Before making significant changes:

1. Read this `AGENTS.md`.
2. Read `PROJECT_PROGRESS.md`.
3. Understand the relevant existing code, architecture, tests, and conventions.
4. Review the current Git branch and working tree.
5. Continue from the documented current state.

Prefer small, safe, targeted changes consistent with existing conventions.
Avoid unrelated refactoring unless it is required for the task.

## Project continuity

After each meaningful implementation, debugging, research,
testing, or validation checkpoint, update `PROJECT_PROGRESS.md`.

Do not update it for trivial actions such as opening files,
searching the repository, or reading documentation.

Keep `PROJECT_PROGRESS.md` concise and sufficient for another AI
agent on another computer to continue without access to the current conversation.

`PROJECT_PROGRESS.md` must contain:

- current status;
- work completed;
- important decisions and assumptions;
- files changed;
- tests, builds, checks, or validation performed and results;
- unresolved issues, risks, or blockers;
- exact recommended next step.

When updating `PROJECT_PROGRESS.md`, preserve still-relevant information
from previous sessions. Remove or replace information only when it is
obsolete, resolved, or superseded.

Before ending meaningful work, verify that `PROJECT_PROGRESS.md`
accurately represents the current repository state.

When appropriate, remind the user to commit and push changes before
switching computers, accounts, sessions, or AI agents.

Never assume conversation history, terminal history, generated files,
or uncommitted local changes will exist on another computer.