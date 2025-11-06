---
description: Build the solution or specific project
---

Build the VerseEngine solution or a specific project.

Usage:
- `/build` - Build entire solution
- `/build [project-name]` - Build specific project (e.g., Verse.ECS, ProjectVerse)

If a project name is provided, build only that project. Otherwise, build the entire solution using `dotnet build Verse.sln`.

After building, report any errors or warnings found. If successful, confirm the build completed.
