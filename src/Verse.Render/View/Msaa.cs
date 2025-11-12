namespace Verse.Render.View;

public enum Msaa
{
	Sample4,
	Off,
	Sample2,
	Sample8,
}

public static class MsaaExtensions
{
	public static int ToSampleCount(this Msaa msaa) => msaa switch {
		Msaa.Off     => 1,
		Msaa.Sample2 => 2,
		Msaa.Sample4 => 4,
		Msaa.Sample8 => 8,
		_            => throw new ArgumentOutOfRangeException(nameof(msaa), msaa, null)
	};
	
	public static Msaa FromSamples(int samples) => samples switch {
		1 => Msaa.Off,
		2 => Msaa.Sample2,
		4 => Msaa.Sample4,
		8 => Msaa.Sample8,
		_ => throw new ArgumentOutOfRangeException(nameof(samples), samples, null)
	};
}