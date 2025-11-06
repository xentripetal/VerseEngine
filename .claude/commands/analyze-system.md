---
description: Analyze a system's dependencies and access patterns
---

Analyze an ECS system to understand its behavior.

If a system name is provided, find and analyze it:
1. What components does it access (read/write)?
2. What resources does it use?
3. What are its dependencies/ordering constraints?
4. Can it run in parallel with other systems?
5. Are there any potential performance concerns?

Provide recommendations for:
- Access pattern optimization
- Parallel execution opportunities
- Component/query improvements
