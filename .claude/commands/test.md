---
description: Run tests for the solution or specific project
---

Run tests for VerseEngine.

Usage:
- `/test` - Run all tests in the solution
- `/test [project-name]` - Run tests for specific project (e.g., Verse.ECS.Test, Verse.Core.Test)

If a project name is provided, run tests for that project only. Otherwise, run all tests using `dotnet test Verse.sln`.

Report:
- Number of tests passed/failed
- Any failing test details
- Overall test summary
