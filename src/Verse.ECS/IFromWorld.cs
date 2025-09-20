namespace Verse.ECS;

public interface IFromWorld<T> where T : IFromWorld<T>
{
	public static abstract T FromWorld(World world);
}

public interface IDefault<T> : IFromWorld<T> where T : IDefault<T>
{
	static T IFromWorld<T>.FromWorld(World world) => T.Default();
	public static abstract T Default();
}