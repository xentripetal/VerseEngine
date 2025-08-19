using System.Text;

namespace Verse.ECS.Generator;

public static class GenHelpers
{
	public static string GenerateSequence(int count, string separator, Func<int, string> generator)
	{
		var sb = new StringBuilder();
		for (var i = 0; i < count; ++i) {
			sb.Append(generator(i));

			if (i < count - 1) {
				sb.Append(separator);
			}
		}

		return sb.ToString();
	}
}