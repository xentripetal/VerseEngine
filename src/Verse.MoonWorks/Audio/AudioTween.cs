using EasingFunction = System.Func<float, float>;

namespace Verse.MoonWorks.Audio;

internal enum AudioTweenProperty
{
	Pan,
	Pitch,
	Volume,
	FilterFrequency,
	Reverb
}

internal class AudioTween
{
	public float DelayTime;
	public float Duration;
	public EasingFunction EasingFunction;
	public float EndValue;
	public AudioTweenProperty Property;
	public float StartValue;
	public float Time;
	public Voice Voice;
}

internal class AudioTweenPool
{
	private readonly Queue<AudioTween> Tweens = new Queue<AudioTween>(16);

	public AudioTweenPool()
	{
		for (var i = 0; i < 16; i += 1) {
			Tweens.Enqueue(new AudioTween());
		}
	}

	public AudioTween Obtain()
	{
		if (Tweens.Count > 0) {
			var tween = Tweens.Dequeue();
			return tween;
		}
		return new AudioTween();
	}

	public void Free(AudioTween tween)
	{
		tween.Voice = null;
		Tweens.Enqueue(tween);
	}
}