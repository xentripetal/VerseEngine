using System.Numerics;
using System.Runtime.InteropServices;

namespace Verse.MoonWorks.Audio;

/// <summary>
///     An emitter for 3D spatial audio.
/// </summary>
public class AudioEmitter : AudioResource
{

	private static readonly float[] stereoAzimuth = new[] {
		0.0f, 0.0f
	};

	private static readonly GCHandle stereoAzimuthHandle = GCHandle.Alloc(
		stereoAzimuth,
		GCHandleType.Pinned
	);
	internal FAudio.F3DAUDIO_EMITTER emitterData;

	public AudioEmitter(AudioDevice device) : base(device)
	{
		emitterData = new FAudio.F3DAUDIO_EMITTER();

		DopplerScale = 1f;
		Forward = -Vector3.UnitZ;
		Position = Vector3.Zero;
		Up = Vector3.UnitY;
		Velocity = Vector3.Zero;

		/* Unexposed variables, defaults based on XNA behavior */
		emitterData.pCone = IntPtr.Zero;
		emitterData.ChannelCount = 1;
		emitterData.ChannelRadius = 1.0f;
		emitterData.pChannelAzimuths = stereoAzimuthHandle.AddrOfPinnedObject();
		emitterData.pVolumeCurve = IntPtr.Zero;
		emitterData.pLFECurve = IntPtr.Zero;
		emitterData.pLPFDirectCurve = IntPtr.Zero;
		emitterData.pLPFReverbCurve = IntPtr.Zero;
		emitterData.pReverbCurve = IntPtr.Zero;
		emitterData.CurveDistanceScaler = 1.0f;
	}

	public float DopplerScale {
		get => emitterData.DopplerScaler;
		set {
			if (value < 0.0f) {
				throw new ArgumentOutOfRangeException("AudioEmitter.DopplerScale must be greater than or equal to 0.0f");
			}
			emitterData.DopplerScaler = value;
		}
	}

	public Vector3 Forward {
		get => new Vector3(
			emitterData.OrientFront.x,
			emitterData.OrientFront.y,
			-emitterData.OrientFront.z
		);
		set {
			emitterData.OrientFront.x = value.X;
			emitterData.OrientFront.y = value.Y;
			emitterData.OrientFront.z = -value.Z;
		}
	}

	public Vector3 Position {
		get => new Vector3(
			emitterData.Position.x,
			emitterData.Position.y,
			-emitterData.Position.z
		);
		set {
			emitterData.Position.x = value.X;
			emitterData.Position.y = value.Y;
			emitterData.Position.z = -value.Z;
		}
	}


	public Vector3 Up {
		get => new Vector3(
			emitterData.OrientTop.x,
			emitterData.OrientTop.y,
			-emitterData.OrientTop.z
		);
		set {
			emitterData.OrientTop.x = value.X;
			emitterData.OrientTop.y = value.Y;
			emitterData.OrientTop.z = -value.Z;
		}
	}

	public Vector3 Velocity {
		get => new Vector3(
			emitterData.Velocity.x,
			emitterData.Velocity.y,
			-emitterData.Velocity.z
		);
		set {
			emitterData.Velocity.x = value.X;
			emitterData.Velocity.y = value.Y;
			emitterData.Velocity.z = -value.Z;
		}
	}
}