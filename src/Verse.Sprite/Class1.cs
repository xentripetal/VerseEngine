using System.Numerics;
using Verse.Assets;
using Verse.MoonWorks.Assets;
using Verse.MoonWorks.Graphics;
using Verse.MoonWorks.Graphics.Resources;

namespace Verse.Sprite;

public struct Sprite
{
	public Handle<Image> Image;
	// todo atlas
	public Color Color;
	public bool FlipX, FlipY;
	/// <summary>
	/// An optional custom size for the sprite that will be used when rendering, instead of the size of the sprite's image.
	/// </summary>
	public Vector2? CustomSize;
	// todo image mode
}

// todo anchor