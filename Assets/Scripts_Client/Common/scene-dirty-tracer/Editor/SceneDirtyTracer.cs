using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

// 씬이 **저절로** 더티(제목 옆 '*')가 될 때, 누가 언제 무엇을 건드렸는지 콘솔에 남긴다.
//
// ■ 무엇을 잡으라고 만들었나
//   씬을 하나도 안 만졌는데 '*'가 붙고, 저장해 봐도 diff 가 0인 증상이다.
//   유니티의 더티 플래그는 "내용이 달라졌다"가 아니라 "누군가 이 씬을 건드렸다고 신고했다"는
//   표시라, 신고만 하고 값은 그대로면 저장해도 바이트가 같아 아무것도 안 잡힌다.
//   범인은 대개 스크립트 쪽이다 — 'EditorUtility.SetDirty'를 조건 없이 부르거나,
//   '[ExecuteAlways]' 컴포넌트가 편집 중에도 돌면서 같은 값을 다시 써 넣는 경우.
//
// ■ 두 갈래로 본다 (하나로는 못 잡는다)
//   [1] 'sceneDirtied'  — 더티가 된 그 순간의 **스택트레이스**. 호출자가 그대로 찍히면 이걸로 끝난다.
//   [2] 'changesPublished' — **무엇이 바뀌었는가**(오브젝트·변경 종류).
//       [1]이 못 잡는 경우를 메운다: 'EditorApplication.delayCall' 안에서 더티가 나면
//       스택에 원래 호출자가 없고, 네이티브 레이아웃 리빌드가 원인이면 아예 관리 코드가 안 찍힌다.
//
// ■ 기본은 꺼짐이다
//   'changesPublished'는 편집 중 상시로 흐르는 이벤트라 켜 두면 콘솔이 도배된다.
//   증상을 재현하는 동안만 켜고 끈다 — 토글은 환경 설정에 있다('SceneDirtyTracerSettings').
[InitializeOnLoad]
internal static class SceneDirtyTracer
{
	static SceneDirtyTracer()
	{
		// 도메인 리로드마다 정적 생성자가 다시 도는데, 그때 구독 상태를 설정값에 맞춘다.
		// (구독은 리로드로 어차피 끊기므로 여기서 다시 걸어 준다)
		Apply(SceneDirtyTracerSettings.Enabled);
	}

	// 구독을 설정값에 맞춘다. 설정 토글과 정적 생성자 양쪽에서 부른다.
	//
	// ※ 걸기 전에 먼저 뗀다 — 두 번 걸리면 같은 줄이 두 번 찍힌다.
	internal static void Apply(bool enabled)
	{
		EditorSceneManager.sceneDirtied     -= OnSceneDirtied;
		ObjectChangeEvents.changesPublished -= OnChangesPublished;

		if (!enabled)
		{
			return;
		}

		EditorSceneManager.sceneDirtied     += OnSceneDirtied;
		ObjectChangeEvents.changesPublished += OnChangesPublished;
	}

	// 씬이 깨끗한 상태에서 더티로 넘어간 순간 (EditorSceneManager.sceneDirtied 구독).
	//
	// ⚠️ 씬마다 '깨끗 → 더티' 전환에서 한 번만 온다. 이미 더티인 씬을 또 건드려도 오지 않으므로,
	//   추적 중에는 씬을 저장해 깨끗하게 만들어 두고 다시 재현해야 한다.
	private static void OnSceneDirtied(UnityEngine.SceneManagement.Scene scene)
	{
		// 'true'는 파일·줄 번호까지 담으라는 뜻 — 어느 스크립트 몇 줄인지가 이 도구의 핵심이다.
		var trace = new StackTrace(true);

		Debug.LogWarning($"[씬 더티 추적] '{scene.name}'이 더티가 됐다.\n{trace}");
	}

	// 에디터가 이번 틱에 모아 둔 변경 목록 (ObjectChangeEvents.changesPublished 구독).
	//
	// 여기 찍힌 오브젝트가 곧 "씬을 건드린 것"이다. 스택트레이스가 유니티 내부만 보여 줄 때
	// 이쪽 이름으로 범인을 좁힌다.
	private static void OnChangesPublished(ref ObjectChangeEventStream stream)
	{
		for (int i = 0; i < stream.length; i++)
		{
			var kind = stream.GetEventType(i);

			Debug.Log($"[씬 더티 추적] {kind} — {Describe(ref stream, i, kind)}");
		}
	}

	// 변경 하나를 사람이 읽을 한 줄로 (OnChangesPublished 에서 호출).
	//
	// ※ 종류마다 인자 구조체가 달라 하나씩 꺼낸다. 다루지 않은 종류는 종류 이름만 남긴다 —
	//   그것만으로도 "어떤 성격의 변경인지"는 알 수 있고, 필요해지면 그때 가지를 늘리면 된다.
	private static string Describe(ref ObjectChangeEventStream stream, int index, ObjectChangeKind kind)
	{
		switch (kind)
		{
			case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
				stream.GetChangeGameObjectOrComponentPropertiesEvent(index, out var properties);

				return NameOf(properties.instanceId);

			case ObjectChangeKind.ChangeGameObjectStructure:
				stream.GetChangeGameObjectStructureEvent(index, out var structure);

				return NameOf(structure.instanceId);

			case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
				stream.GetChangeGameObjectStructureHierarchyEvent(index, out var hierarchy);

				return NameOf(hierarchy.instanceId);

			case ObjectChangeKind.ChangeGameObjectParent:
				stream.GetChangeGameObjectParentEvent(index, out var reparent);

				return $"{NameOf(reparent.instanceId)} : " +
					   $"{NameOf(reparent.previousParentInstanceId)} → {NameOf(reparent.newParentInstanceId)}";

			case ObjectChangeKind.CreateGameObjectHierarchy:
				stream.GetCreateGameObjectHierarchyEvent(index, out var created);

				return NameOf(created.instanceId);

			case ObjectChangeKind.DestroyGameObjectHierarchy:
				stream.GetDestroyGameObjectHierarchyEvent(index, out var destroyed);

				// 이미 지워진 오브젝트라 이름을 못 꺼낸다 — 부모라도 남긴다.
				return $"부모 {NameOf(destroyed.parentInstanceId)} 아래에서 삭제";

			default:
				return "(상세 없음)";
		}
	}

	// 인스턴스 ID를 알아볼 수 있는 계층 경로로 (Describe 에서 호출).
	//
	// 이름만으로는 같은 이름이 여럿일 때 못 고른다 — 부모를 몇 단계 붙여 준다.
	//
	// ⚠️ ID로 오브젝트를 찾는 API의 이름이 유니티 6.3에서 갈렸다. 'InstanceIDToObject'는 거기서
	//   obsolete가 되고 'EntityIdToObject'로 넘어갔는데, 그 새 이름은 6.3 **이전에는 아예 없다.**
	//   한쪽만 쓰면 다른 버전에서 경고 아니면 컴파일 에러가 나므로 갈라 둔다.
	private static string NameOf(int instanceId)
	{
#if UNITY_6000_3_OR_NEWER
		var target = EditorUtility.EntityIdToObject(instanceId);
#else
		var target = EditorUtility.InstanceIDToObject(instanceId);
#endif

		if (target == null)
		{
			return $"(사라진 오브젝트 #{instanceId})";
		}

		var node = target as GameObject;

		if (node == null && target is Component component)
		{
			// 컴포넌트면 어느 오브젝트의 무슨 컴포넌트인지 둘 다 알려 준다.
			return $"{PathOf(component.transform)} [{component.GetType().Name}]";
		}

		return node != null ? PathOf(node.transform) : $"{target.name} [{target.GetType().Name}]";
	}

	// 알아볼 수 있을 만큼만 계층 경로를 만든다 — 뿌리까지 다 붙이면 한 줄이 너무 길어진다 (NameOf 에서 호출)
	private static string PathOf(Transform node)
	{
		string path = node.name;

		Transform parent = node.parent;

		for (int depth = 0; depth < 3 && parent != null; depth++, parent = parent.parent)
		{
			path = parent.name + "/" + path;
		}

		return path;
	}
}
