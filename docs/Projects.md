# VerseEngine Project Structure

## Overview

VerseEngine is organized into multiple .NET projects, each with a specific purpose. This document describes the role and dependencies of each project.

## Core Foundation

### Verse.Core

**Purpose**: Foundation layer of the game engine

**Provides**:
- SDL environment integration
- Basic ECS implementation
- Plugin system for extensibility
- Application lifecycle management (App class)
- Execution framework and scheduling
- SubApp support for parallel worlds

**Dependencies**: None (foundation layer)

**Key Types**:
- `App` - Main application class
- `IPlugin` - Plugin interface
- Core scheduling infrastructure

### Verse.ECS

**Purpose**: Advanced Entity Component System implementation

**Provides**:
- Entity management with archetypes
- Component registry and storage
- Query system with read/write access control
- System scheduling and parallel execution
- Resource management (global singletons)
- Command buffers for deferred operations
- Change detection and tick tracking
- Multiple storage backends (Table, SparseSet)

**Dependencies**: Verse.Core

**Key Directories**:
- `Scheduling/` - System scheduling and execution
- `Systems/` - System implementations and SystemSets
- `Storage/` - Component storage backends
- `Datastructures/` - ECS-specific data structures

**Key Types**:
- `World` - ECS world container
- `Query` - Component queries
- `ComponentRegistry` - Component type management
- `Archetype` - Entity storage by component composition

## Graphics & Rendering

### Verse.MoonWorks

**Purpose**: Low-level graphics, audio, and input wrapper

**Provides**:
- MoonWorks/SDL3 integration
- Graphics pipeline (Vulkan, Metal, DirectX backends)
- Shader management (SPIR-V)
- Audio system (FAudio integration)
- Input handling (keyboard, mouse, gamepad)
- Window management
- AsyncIO for file operations
- Embedded stock shaders (fullscreen, video, text/MSDF)

**Dependencies**: Verse.Core, Verse.Moonworks.Native

**Key Features**:
- Multi-backend graphics support
- Resource management (textures, buffers, shaders)
- Audio playback and mixing
- Input state tracking

### Verse.Moonworks.Native

**Purpose**: Native interop layer for MoonWorks

**Provides**:
- P/Invoke declarations
- Native library bindings
- Low-level SDL3 and FAudio wrappers

**Dependencies**: None (native interop)

### Verse.Render

**Purpose**: High-level rendering system

**Provides**:
- Render batching for performance
- Camera systems and view management
- Render graphs for complex pipelines
- Render pipeline abstractions
- 2D rendering pipeline
- View and projection management

**Dependencies**: Verse.Core, Verse.ECS, Verse.MoonWorks

**Key Concepts**:
- Render batching reduces draw calls
- Camera components control view
- Render graphs define rendering order
- Pipeline management for different render passes

### Verse.Sprite

**Purpose**: Sprite data management

**Provides**:
- Sprite component definitions
- Sprite atlas support
- Sprite metadata

**Dependencies**: Verse.ECS

### Verse.SpriteRenderer

**Purpose**: 2D sprite rendering implementation

**Provides**:
- Sprite rendering systems
- Batch rendering for sprites
- Integration with Verse.Render

**Dependencies**: Verse.Sprite, Verse.Render, Verse.Transform

## Support Systems

### Verse.Math

**Purpose**: Mathematics library for game development

**Provides**:
- Vector types (Vector2, Vector3, Vector4)
- Matrix types (Matrix4, transforms)
- Quaternions for rotations
- Common math utilities
- SIMD optimizations (where applicable)

**Dependencies**: None

**Future**: Backlog includes rewriting Matrix4 to Affine3 with SIMD

### Verse.Transform

**Purpose**: Transform and positioning system

**Provides**:
- Transform component
- Position, rotation, scale management
- Hierarchical transforms (parent/child relationships)
- Transform systems for hierarchy updates

**Dependencies**: Verse.ECS, Verse.Math

### Verse.Assets

**Purpose**: Asset loading and management

**Provides**:
- Asset loading system
- Asset handles for type-safe access
- Hot-reload support
- SDL storage integration
- C# filesystem access for development

**Dependencies**: Verse.Core, Verse.ECS

**Status**: Active development (see backlog)
- Asset loader system in progress
- Dual loading support (SDL + filesystem) planned

### Verse.Tracing

**Purpose**: Performance profiling and debugging

**Provides**:
- Performance tracing
- System execution timing
- Frame profiling
- Debug utilities

**Dependencies**: Verse.Core

**Usage**:
```csharp
using (Tracer.Begin("MySystem"))
{
    // Code to profile
}
```

### Verse.Editor

**Purpose**: Built-in runtime editor

**Provides**:
- ImGui-based editor interface
- Entity inspector
- Component editing
- System profiling
- Runtime game state modification

**Dependencies**: Verse.Core, Verse.ECS

**Usage**: Add EditorPlugin to app to enable

## Code Generation

### Generators/Verse.ECS.Generator

**Purpose**: Public code generator for ECS systems

**Provides**:
- System wrapper generation
- Automatic dependency injection
- Query setup code
- Read/write access tracking

**Dependencies**: Roslyn, Verse.ECS

**Type**: Source generator (runs at compile time)

### Generators/Verse.ECS.Internal.Generator

**Purpose**: Internal code generators

**Provides**:
- Internal ECS codegen
- Roslyn analyzers
- Advanced generation for ECS internals

**Dependencies**: Roslyn

**Type**: Source generator and analyzer

## Test Projects

### test/Verse.Core.Test
Unit tests for Verse.Core functionality

### test/Verse.ECS.Test
ECS system and archetype tests

### test/Verse.Render.Tests
Rendering system tests

### test/Verse.Assets.Test
Asset loading tests

### test/Verse.Benchmarks
Performance benchmarks for critical paths

## Example Projects

### game/ProjectVerse

**Purpose**: Example game demonstrating engine usage

**Shows**:
- App setup and configuration
- Plugin usage
- System creation
- Asset loading
- Rendering pipeline
- Best practices

**Entry Point**: `game/ProjectVerse/Program.cs`

## Dependency Graph

```
                                    ┌─────────────┐
                                    │ Verse.Core  │
                                    │  (Foundation)│
                                    └──────┬──────┘
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    │                      │                      │
            ┌───────▼────────┐    ┌───────▼────────┐    ┌───────▼────────┐
            │  Verse.ECS     │    │ Verse.Tracing  │    │ Verse.Editor   │
            │  (ECS Core)    │    │                │    │                │
            └───────┬────────┘    └────────────────┘    └────────────────┘
                    │
        ┌───────────┼───────────┬─────────────┐
        │           │           │             │
┌───────▼─────┐ ┌──▼─────┐ ┌───▼────────┐ ┌─▼──────────┐
│Verse.Assets │ │V.Math  │ │V.Transform │ │ V.Sprite   │
└─────────────┘ └────────┘ └────────────┘ └─────┬──────┘
                                                 │
                    ┌────────────────────────────┘
                    │
            ┌───────▼──────────┐
            │ Verse.MoonWorks  │ ◄────┐
            │   (Graphics)     │      │
            └────────┬─────────┘      │
                     │                │
            ┌────────▼────────┐  ┌────▼───────────────┐
            │  Verse.Render   │  │V.Moonworks.Native  │
            │  (High-level)   │  │   (Interop)        │
            └────────┬────────┘  └────────────────────┘
                     │
            ┌────────▼────────────┐
            │ Verse.SpriteRenderer│
            └─────────────────────┘
```

## Module Guidelines

### Adding New Projects

When creating a new project:
1. Choose appropriate layer (Core, ECS, Graphics, Support)
2. Add only necessary dependencies
3. Avoid circular dependencies
4. Update this documentation
5. Add to solution file
6. Create corresponding tests

### Dependency Rules

- **Core layer** (Verse.Core, Verse.Math): No dependencies
- **ECS layer** (Verse.ECS): Depends only on Core
- **Feature modules**: Depend on Core and/or ECS
- **High-level modules**: Can depend on multiple lower layers
- **Test projects**: Can depend on any module

### Never:
- Create circular dependencies
- Have core modules depend on feature modules
- Bypass abstraction layers

## Building

Build entire solution:
```bash
dotnet build Verse.sln
```

Build specific project:
```bash
dotnet build src/Verse.ECS/Verse.ECS.csproj
```

Run tests:
```bash
dotnet test
```

## Project Naming

- **Verse.*** - Core engine modules
- **Verse.*.Test** - Test projects
- **Game projects** - No prefix (e.g., ProjectVerse)
- **Generators** - In Generators/ folder

## Future Modules

Planned (from backlog):
- State management system
- Events system rework
- Observer behaviors
- Enhanced asset streaming

See `docs/todo/Backlog.md` for current roadmap.
