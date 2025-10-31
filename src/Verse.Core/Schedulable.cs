namespace Verse.Core;

public interface ISchedulable
{
	/// <summary>
	/// Schedule any systems in this object onto the app
	/// </summary>
	/// <param name="app">App to schedule on</param>
	/// <returns>Scheduled app</returns>
	public App Schedule(App app);
}
