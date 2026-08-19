using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 에디터가 지금 쓰고 있는 메모리를 읽고, 요청이 오면 정리한다(동작만, UI 없음).
	/// 화면 표시는 'EditorMemoryToolbarButton'이 맡는다.
	/// </summary>
	internal static class EditorMemoryMeter
	{
		/// <summary>한 시점의 메모리 수치 묶음.</summary>
		internal readonly struct Snapshot
		{
			/// <summary>에디터 프로세스가 실제로 물고 있는 물리 메모리 — 작업 관리자의 'Unity Editor'와 같은 값.</summary>
			public readonly long ProcessBytes;

			/// <summary>Unity 네이티브가 지금 데이터를 담고 있는 양(에셋·씬 등).</summary>
			public readonly long UnityAllocated;

			/// <summary>Unity 네이티브가 OS에서 미리 받아 둔 자리(할당보다 크거나 같다).</summary>
			public readonly long UnityReserved;

			/// <summary>C# 스크립트(Mono) 힙에서 실제로 쓰는 양.</summary>
			public readonly long MonoUsed;

			/// <summary>C# 스크립트(Mono) 힙 전체 크기.</summary>
			public readonly long MonoHeap;

			/// <summary>그래픽 드라이버가 잡고 있는 양(텍스처·메시 등).</summary>
			public readonly long GraphicsDriver;

			public Snapshot(long processBytes, long unityAllocated, long unityReserved,
			                long monoUsed,     long monoHeap,       long graphicsDriver)
			{
				ProcessBytes   = processBytes;
				UnityAllocated = unityAllocated;
				UnityReserved  = unityReserved;
				MonoUsed       = monoUsed;
				MonoHeap       = monoHeap;
				GraphicsDriver = graphicsDriver;
			}
		}

		/// <summary>지금 이 순간의 메모리 수치를 읽어 온다 (툴바 갱신·정리 로그가 호출).</summary>
		public static Snapshot Take()
		{
			return new Snapshot
				(ReadProcessBytes(),
				 Profiler.GetTotalAllocatedMemoryLong(),
				 Profiler.GetTotalReservedMemoryLong(),
				 Profiler.GetMonoUsedSizeLong(),
				 Profiler.GetMonoHeapSizeLong(),
				 Profiler.GetAllocatedMemoryForGraphicsDriver());
		}

		/// <summary>바이트 수를 유니티 표기('1.4 GB')로 바꾼다.</summary>
		public static string Format(long bytes) => EditorUtility.FormatBytes(bytes);

		/// <summary>미사용 에셋 언로드 + GC 수집으로 메모리를 정리하고 줄어든 양을 콘솔에 남긴다 (툴바 버튼 클릭).</summary>
		public static void Cleanup()
		{
			long before = ReadProcessBytes();

			// 에디터에선 'Resources.UnloadUnusedAssets()'가 비동기라 결과를 바로 못 잰다 — 즉시판을 쓴다.
			EditorUtility.UnloadUnusedAssetsImmediate();

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();   // 파이널라이저가 붙잡고 있던 것까지 마저 회수

			long after   = ReadProcessBytes();
			long freed   = before - after;
			string delta = freed >= 0 ? $"-{Format(freed)}" : $"+{Format(-freed)}";

			Debug.Log($"[메모리] {Format(before)} → {Format(after)} ({delta})");
		}

		#region 프로세스 메모리 읽기 (Win32)

		// ★ 'System.Diagnostics.Process.WorkingSet64'는 유니티의 Mono에서 현재 프로세스에 대해
		//   **0을 돌려준다**(실측). 그래서 OS에 직접 물어본다. Windows 전용이며, 실패하면 0을 준다.
		[StructLayout(LayoutKind.Sequential)]
		private struct ProcessMemoryCounters
		{
			public uint    cb;                          // 이 구조체의 크기. 호출 전에 채워야 한다
			public uint    pageFaultCount;
			public UIntPtr peakWorkingSetSize;
			public UIntPtr workingSetSize;              // 지금 물리 메모리에 올라가 있는 양 = 우리가 쓰는 값
			public UIntPtr quotaPeakPagedPoolUsage;
			public UIntPtr quotaPagedPoolUsage;
			public UIntPtr quotaPeakNonPagedPoolUsage;
			public UIntPtr quotaNonPagedPoolUsage;
			public UIntPtr pagefileUsage;
			public UIntPtr peakPagefileUsage;
			public UIntPtr privateUsage;
		}

		[DllImport("psapi.dll", SetLastError = true)]
		private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		private static bool _isNativeReadBroken;   // 한 번 실패하면 다시 시도하지 않는다(1초마다 도는 자리다)

		// 에디터 프로세스가 실제로 쓰는 물리 메모리를 OS에서 읽는다
		private static long ReadProcessBytes()
		{
			if (_isNativeReadBroken)
			{
				return 0;
			}

			try
			{
				ProcessMemoryCounters counters = default;
				counters.cb = (uint)Marshal.SizeOf<ProcessMemoryCounters>();

				if (GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.cb))
				{
					return (long)counters.workingSetSize.ToUInt64();
				}
			}
			catch (Exception exception)
			{
				// Windows가 아니거나 psapi를 못 찾은 경우 — 한 번만 알리고 이후엔 조용히 0을 준다.
				Debug.LogWarning($"[메모리] 프로세스 메모리를 읽지 못해 표시를 끈다: {exception.Message}");
				_isNativeReadBroken = true;
			}

			return 0;
		}

		#endregion
	}
}
