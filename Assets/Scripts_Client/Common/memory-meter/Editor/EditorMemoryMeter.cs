using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

// 에디터가 지금 쓰고 있는 메모리를 읽고, 요청이 오면 정리한다(동작만, UI 없음).
// 화면 표시는 'EditorMemoryToolbarButton'이 맡는다.
internal static class EditorMemoryMeter
{
	// 한 시점의 메모리 수치 묶음.
	// ★ 용어 — '확보(Reserved)'는 Win32의 예약(Reserve)이 아니라 유니티 할당자가 OS에서 커밋해 받아 둔 풀이고,
	//   '할당(Allocated)'은 그 풀에서 실제로 나눠 준 양이다. 둘 다 물리 RAM 적재 여부와는 무관하다.
	//   'ProcessBytes'(워킹셋)와는 배타 관계가 아니라 서로 겹친다.
	//   정의·포함 관계는 'memory-meter 규칙.md'의 "용어" 절에 표로 있다.
	internal readonly struct Snapshot
	{
		// 에디터 프로세스가 지금 물리 RAM에 올려 둔 양(워킹셋) — 아래 항목들을 더한 값이 아니다.
		// ★ 이 값은 **해제가 없어도 크게 오르내린다.** 메모리 압박·창 최소화 때 OS가 안 쓰는
		//   페이지를 워킹셋에서 빼내(트림) 대기(Standby) 리스트로 돌리기 때문이다. 그 페이지는
		//   RAM에 그대로 남아 있어 다시 건드리면 곧바로 돌아온다 — 줄었다고 회수된 게 아니다.
		public readonly long ProcessBytes;

		// Unity 네이티브 풀에서 실제로 나눠 준 양(에셋·씬 등) — 할당.
		public readonly long UnityAllocated;

		// Unity 네이티브가 OS에서 커밋해 받아 둔 풀 — 확보(할당보다 크거나 같다).
		public readonly long UnityReserved;

		// C# 스크립트(Mono) 힙에서 실제로 나눠 준 양 — 할당.
		public readonly long MonoUsed;

		// C# 스크립트(Mono) 힙 전체 크기 — 확보.
		public readonly long MonoHeap;

		// 그래픽 드라이버가 잡고 있는 양(텍스처·메시 등) — VRAM 쪽이라 워킹셋엔 아예 안 잡힌다.
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

	// 지금 이 순간의 메모리 수치를 읽어 온다 (툴바 갱신·정리 로그가 호출).
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

	// 바이트 수를 유니티 표기('1.4 GB')로 바꾼다.
	public static string Format(long bytes) => EditorUtility.FormatBytes(bytes);

	// 미사용 에셋 언로드 + GC 수집으로 메모리를 정리하고 줄어든 양을 콘솔에 남긴다 (툴바 버튼 클릭).
	// ★ 전후를 워킹셋으로 재므로 **델타가 '+'(증가)로 찍힐 수 있다** — 오류가 아니다.
	//   GC가 회수한 자리는 Mono의 free list로 갈 뿐이고, 물리에서 빠지는 건 나중에 OS가
	//   트림할 때다. 반면 정리하느라 훑은 페이지는 그 자리에서 워킹셋에 올라온다.
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
