using System.Numerics;
using Verse.Math;

namespace Verse.Math.Test;

public class MatrixVerificationTest
{
	// Simple tests to make sure the outputs match what Bevy creates
	[Fact]
	public void VerifyTransform()
	{
		var m = new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
		var p = new Vector3(17, 18, 19);
		var projected = m.TransformPoint(p);
		Assert.Equal(new Vector3(291, 346, 401), projected);
	}
	
	[Fact]
	public void VerifyProject()
	{
		var m = new Matrix4x4(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
		var p = new Vector3(17, 18, 19);
		var projected = m.ProjectPoint(p);
		
		Assert.Equal(new Vector3(0.6381579f, 0.75877196f, 0.87938595f), projected);
	}
}