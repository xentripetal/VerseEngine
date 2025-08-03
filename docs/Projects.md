# Dotnet Project Structure

Verse.Core is the foundation of the game engine. It provides a SDL environment, basic ECS implementation, a plugin system, and an
execution framework. All other repositories depend on Verse.Core.

Verse.Editor is a built-in imgui based editor for Verse.Core. It allows you to inspect and modify the game state at
runtime. 

Verse.Graphics.Native provides