namespace Verse.ECS;

public record struct ResourceTicks
{
	public ResourceTicks(BoxedTick addedTick, BoxedTick changedTick)
	{
		Added = addedTick;
		Changed = changedTick;
	}
	public BoxedTick Added;
	public BoxedTick Changed;
}

public class BoxedTick
{
	public BoxedTick(Tick tick)
	{
		Tick = tick;
	}
	public BoxedTick()
	{
		Tick = new Tick(0);
	}
	public Tick Tick;
	
	public static implicit operator Tick(BoxedTick boxed)
	{
		return boxed.Tick;
	}
	
	public static implicit operator BoxedTick(Tick tick)
	{
		return new BoxedTick(tick);
	}
	
	public Tick Get()
	{
		return Tick;
	}
	
	public void Set(Tick tick)
	{
		Tick = tick;
	}
}

public struct Ticks
{
	public BoxedTick Added;
	public BoxedTick Changed;
	public Tick ThisRun;
	public Tick LastRun;
}


public struct Tick
{
	public Tick(uint tick)
	{
		this.tick = tick;
	}
	
	private uint tick;
	
	public static readonly Tick Max = new Tick(uint.MaxValue);
	/// <summary>
	/// The (arbitrarily chosen) minimum number of world tick increments between `check_tick` scans.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Change ticks can only be scanned when systems aren't running. Thus, if the threshold is `N`,
	/// the maximum is `2 * N - 1` (i.e. the world ticks `N - 1` times, then `N` times).
	/// </para>
	/// <para>
	/// If no change is older than `u32::MAX - (2 * N - 1)` following a scan, none of their ages can
	/// overflow and cause false positives.
	/// </para>
	/// </remarks>
	public const uint CHECK_TICK_THRESHOLD = 518_400_000;
	/// <summary>
	/// The maximum change tick different that won't overflow before the next `check_tick` scan.
	/// </summary>
	/// <remarks>Changes stop being detected once they become this old</remarks>
	public const uint MAX_CHANGE_AGE = uint.MaxValue - (2 * CHECK_TICK_THRESHOLD - 1);
	
	public uint Value => tick;

	public void Set(uint tick)
	{
		this.tick = tick;
	}

	/// <summary>
	/// Returns true if this tick is newer occurred since <paramref name="lastRun"/>
	/// </summary>
	/// <param name="lastRun">The last time a given context has ran</param>
	/// <param name="thisRun">The current tick of the context, used as a reference to help deal with wraparound</param>
	/// <returns>true if newer than lastRun</returns>
	public bool IsNewerThan(Tick lastRun, Tick thisRun)
	{
		var ticksSinceInsert = uint.Min(thisRun.RelativeTo(this).tick, MAX_CHANGE_AGE);
		var ticksSinceContext = uint.Min(thisRun.RelativeTo(lastRun).tick, MAX_CHANGE_AGE);
		
		return ticksSinceContext > ticksSinceInsert;
	}
	

	public Tick RelativeTo(Tick other)
	{
		return new Tick(tick - other.tick);
	}

	/// <summary>
	/// Wraps this change ticks' value if it exceeds <see cref="Max"/>
	/// </summary>
	/// <param name="check">Reference tick</param>
	/// <returns>True if wrapping was performed, otherwise false.</returns>
	public bool CheckTick(Tick check)
	{
		var age = check.RelativeTo(this);
		if (age.Value > Tick.Max.Value) {
			tick = check.RelativeTo(Max).tick;
			return true;
		}
		return false;
	}
	
	public static implicit operator uint(Tick tick) => tick.tick;
	public static implicit operator Tick(uint tick) => new Tick(tick);
}