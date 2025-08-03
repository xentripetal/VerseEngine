using System.Collections.Concurrent;

namespace Verse.MoonWorks.Storage;

/// <summary>
///     The result of a user storage operation.
/// </summary>
public enum Result
{
	/// <summary>
	///     The operation is not yet complete.
	/// </summary>
	Pending,

	/// <summary>
	///     The operation completed and was successful.
	/// </summary>
	Success,

	/// <summary>
	///     The operation failed.
	/// </summary>
	Failure
}

/// <summary>
///     Contains data about an asynchronous user storage operation.
/// </summary>
public class ResultToken
{

	/// <summary>
	///     The buffer result of a ReadFile operation.
	/// </summary>
	public IntPtr Buffer;
	public Result Result;

	/// <summary>
	///     The size result of a GetSpaceRemaining or GetFileSize operation.
	/// </summary>
	public ulong Size;
}

internal class ResultTokenPool
{
	private readonly ConcurrentQueue<ResultToken> Tokens = new ConcurrentQueue<ResultToken>();

	public ResultToken Obtain()
	{
		if (Tokens.TryDequeue(out var token)) {
			return token;
		}
		return new ResultToken();
	}

	public void Return(ResultToken token)
	{
		token.Result = Result.Pending;
		token.Size = 0;
		token.Buffer = IntPtr.Zero;
		Tokens.Enqueue(token);
	}
}