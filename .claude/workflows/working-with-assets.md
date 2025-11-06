# Workflow: Working with Assets

Guide for loading and managing assets in VerseEngine.

## Overview

The asset system (`Verse.Assets`) handles loading resources like textures, sprites, shaders, and audio from disk or SDL storage.

## Asset Types

Common asset types:
- **Textures** - Images for rendering
- **Sprites** - 2D graphical elements
- **Shaders** - GPU programs (SPIR-V)
- **Audio** - Sound effects and music
- **Data Files** - JSON, XML, custom formats

## Step 1: Define Asset Resource

Create a resource to hold your asset handles:

```csharp
public class GameAssets
{
    public AssetHandle<Texture> PlayerSprite { get; set; }
    public AssetHandle<Shader> CustomShader { get; set; }
    public AssetHandle<AudioClip> BackgroundMusic { get; set; }
}
```

## Step 2: Initialize Asset Resource

Register resource with the App:

```csharp
var app = App.Default();
app.InitResource<GameAssets>();
```

## Step 3: Load Assets

### Loading in System

```csharp
public static void LoadAssets(
    ResMut<GameAssets> assets,
    Res<AssetManager> manager)
{
    assets.Value.PlayerSprite = manager.Load<Texture>("sprites/player.png");
    assets.Value.CustomShader = manager.Load<Shader>("shaders/custom.spv");
}
```

### Loading at Startup

```csharp
public class GameAssets : IFromWorld
{
    public static GameAssets FromWorld(World world)
    {
        var manager = world.GetResource<AssetManager>();
        return new GameAssets
        {
            PlayerSprite = manager.Load<Texture>("sprites/player.png")
        };
    }
}
```

## Step 4: Use Assets in Systems

```csharp
public static void RenderSprites(
    Query<Transform, Sprite> query,
    Res<GameAssets> assets,
    ResMut<Renderer> renderer)
{
    foreach (var (transform, sprite) in query.Iter())
    {
        var texture = assets.Value.PlayerSprite.Get();
        renderer.DrawSprite(texture, transform.Position);
    }
}
```

## Asset Loading Patterns

### Pattern 1: Lazy Loading

Load assets when first needed:

```csharp
public class AssetCache
{
    private Dictionary<string, AssetHandle<Texture>> _cache = new();

    public AssetHandle<Texture> GetOrLoad(AssetManager manager, string path)
    {
        if (!_cache.ContainsKey(path))
        {
            _cache[path] = manager.Load<Texture>(path);
        }
        return _cache[path];
    }
}
```

### Pattern 2: Preloading

Load all assets at startup:

```csharp
public static void PreloadAssets(
    ResMut<GameAssets> assets,
    Res<AssetManager> manager)
{
    // Load all game assets upfront
    assets.Value.LoadAll(manager);
}
```

### Pattern 3: Streaming

Load assets in background:

```csharp
public static void StreamAssets(
    Commands commands,
    Query<NeedsAsset> query,
    Res<AssetManager> manager)
{
    foreach (var (needsAsset, entity) in query.IterWithEntity())
    {
        // Start async load
        var handle = manager.LoadAsync<Texture>(needsAsset.Path);

        commands.Remove<NeedsAsset>(entity);
        commands.Insert(entity, new LoadingAsset { Handle = handle });
    }
}
```

## Hot Reloading

The asset system supports hot reloading from the C# filesystem:

```csharp
public static void CheckAssetChanges(
    Res<AssetManager> manager,
    ResMut<GameAssets> assets)
{
    if (manager.HasChanged("sprites/player.png"))
    {
        assets.Value.PlayerSprite.Reload();
    }
}
```

## Asset Storage Locations

### SDL Storage
- Embedded resources
- Platform-specific locations
- Use for shipped game assets

### C# Filesystem
- Direct file access
- Use for development/hot-reload
- Path: relative to working directory

## Error Handling

### Check Asset Validity

```csharp
if (!assetHandle.IsValid())
{
    Log.Warning($"Asset failed to load: {path}");
    return;
}
```

### Fallback Assets

```csharp
public class GameAssets
{
    public AssetHandle<Texture> ErrorTexture { get; set; }

    public AssetHandle<Texture> GetTextureOrDefault(string path, AssetManager manager)
    {
        var handle = manager.Load<Texture>(path);
        return handle.IsValid() ? handle : ErrorTexture;
    }
}
```

## Best Practices

1. **Centralize asset references** - Use resource classes
2. **Validate on load** - Check asset handles
3. **Provide fallbacks** - Error textures, default sounds
4. **Batch loads** - Load related assets together
5. **Unload unused assets** - Free memory when done
6. **Use asset handles** - Don't store raw asset data

## Common Issues

### Asset not found
- Check path is relative to working directory
- Verify file exists in expected location
- Check file permissions

### Performance issues
- Avoid loading assets every frame
- Cache asset handles in resources
- Use async loading for large assets
- Consider asset streaming for open worlds

### Memory leaks
- Release asset handles when done
- Clear caches periodically
- Monitor asset manager statistics

## Current Limitations

From backlog:
- Asset loader system is in development
- SDL storage + C# filesystem support coming
- Hot reload improvements planned

## Example: Complete Asset Workflow

```csharp
// 1. Define asset resource
public class GameAssets : IFromWorld
{
    public AssetHandle<Texture> PlayerTexture { get; set; }
    public AssetHandle<Shader> SpriteShader { get; set; }

    public static GameAssets FromWorld(World world)
    {
        var manager = world.GetResource<AssetManager>();
        return new GameAssets
        {
            PlayerTexture = manager.Load<Texture>("player.png"),
            SpriteShader = manager.Load<Shader>("sprite.spv")
        };
    }
}

// 2. Register in app
var app = App.Default();
app.AddPlugin(new AssetPlugin());
app.InitResource<GameAssets>();

// 3. Use in systems
public static void RenderPlayer(
    Query<Player, Transform> query,
    Res<GameAssets> assets,
    ResMut<Renderer> renderer)
{
    var texture = assets.Value.PlayerTexture.Get();
    foreach (var (_, transform) in query.Iter())
    {
        renderer.Draw(texture, transform.Position);
    }
}
```
