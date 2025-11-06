---
description: Guide for creating a new ECS system
---

Guide the user through creating a new ECS system.

Ask the user:
1. What should the system be called?
2. What components does it need to access?
3. What resources does it need (if any)?
4. Should it be a method-based or class-based system?
5. Which SystemSet should it belong to?

Then create the system following VerseEngine conventions:
- Use proper naming (e.g., UpdateVelocitySystem or UpdateVelocity)
- Set up correct query with read/write access
- Add necessary resource parameters
- Include XML documentation
- Add to appropriate SystemSet

Show the user example code and ask where they want it created.
