# VerseEngine - Project Overview

## What is VerseEngine?

VerseEngine is a **high-performance C# game engine** built on .NET 10 with a focus on modern Entity Component System (ECS) architecture. The engine is designed to provide blazing-fast performance through cache-friendly data layouts while maintaining developer-friendly APIs.

## Core Philosophy

- **Performance First**: ECS architecture with archetype-based storage for optimal cache usage
- **Modern C#**: Leverages .NET 10 features, AOT-ready for cross-platform deployment
- **Developer Experience**: Strong typing, code generation, built-in editor, and clear APIs
- **Modular Design**: Clear separation of concerns across 14+ specialized modules

## Technology Stack

- **Language**: C# (.NET 10.0)
- **Architecture**: Entity Component System (ECS) inspired by Bevy and Unity DOTS
- **Graphics**: MoonWorks (SDL3-based framework supporting Vulkan, Metal, DirectX)
- **Audio**: FAudio integration
- **Input**: SDL3 input handling
- **Editor**: ImGui-based runtime editor

## Project Structure

### Core Modules

- **Verse.Core** - Foundation layer providing SDL environment, basic ECS, plugin system, and app lifecycle
- **Verse.ECS** - Advanced ECS framework with queries, archetypes, systems, and scheduling
- **Verse.Editor** - Built-in ImGui editor for runtime inspection and modification

### Graphics & Rendering

- **Verse.MoonWorks** - Low-level graphics wrapper around MoonWorks/SDL3
- **Verse.Render** - High-level rendering system with batching, cameras, and render graphs
- **Verse.Sprite** / **Verse.SpriteRenderer** - 2D sprite management and rendering

### Support Systems

- **Verse.Math** - Mathematics library
- **Verse.Transform** - Transform/positioning system
- **Verse.Assets** - Asset loading and management
- **Verse.Tracing** - Performance profiling and tracing

### Code Generation

- **Verse.ECS.Generator** - Public code generator for system creation
- **Verse.ECS.Internal.Generator** - Internal Roslyn-based code generators

## Key Concepts

### Entity Component System (ECS)

- **Entities**: Lightweight identifiers for game objects
- **Components**: Data attached to entities (can be classes or structs)
- **Resources**: Global singleton state (can be classes or structs)
- **Systems**: Logic that operates on entities with specific components
- **Queries**: Type-safe component access with read/write permissions
- **Archetypes**: Internal entity grouping by component composition for cache efficiency

### System Organization

- **SystemSets**: Group systems for execution order control
- **Scheduling**: Automatic parallel execution based on component access patterns
- **ApplyDeferred**: Command buffer flushing between system stages

### Asset Management

- Assets are loaded through the asset system
- Support for both SDL storage and C# filesystem for hot-reloading
- Asset handles provide type-safe access

## Common Workflows

### Adding a New System

1. Create a method or class that implements your logic
2. Use code generation attributes or manual system creation
3. Register system in a SystemSet
4. Schedule with appropriate ordering constraints

### Adding a Component

1. Define struct or class for component data
2. Add to entity using World API
3. Query for component in systems
4. Components can be configured for different storage backends (Table/SparseSet)

### Rendering Pipeline

1. Create/update sprite or renderable components
2. Render systems process visible entities
3. Render graph executes draw calls
4. Camera systems control view transforms

## Testing

- **Verse.Core.Test**: Core functionality tests
- **Verse.ECS.Test**: ECS framework tests
- **Verse.Render.Tests**: Rendering system tests
- **Verse.Assets.Test**: Asset loading tests
- **Verse.Benchmarks**: Performance benchmarks

## Example Project

**ProjectVerse** (`/game/ProjectVerse/`) demonstrates engine usage with a working game application.

## Development Status

Active development with regular improvements to:
- ECS system capabilities (recent: class components, struct resources)
- Asset loading system
- Rendering pipeline
- Code generation improvements

See `docs/todo/Backlog.md` for current feature roadmap.

## Important File Locations

- **Main Application**: `src/Verse.Core/App.cs`
- **ECS World**: `src/Verse.ECS/World.cs`
- **Component System**: `src/Verse.ECS/Components.cs`
- **Query System**: `src/Verse.ECS/Query.cs`
- **System Scheduling**: `src/Verse.ECS/Scheduling/`
- **Example Game**: `game/ProjectVerse/Program.cs`

## Git Workflow

- Main development branch: `main`
- Feature branches use pattern: `claude/<feature-name>-<session-id>`
- Standard commit/push workflow with descriptive messages
