using System;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 인벤토리 비우기 — 종류 · 보관함을 골라 한 번에 지운다 (T-213 · 이슈 #154).
	// 서버 'ClearItems'(Arg1 = 0 전체 · 1 자원 · 2 장비 · 3 캐릭터 · Arg2 = 0 인벤토리 · 1 창고 · 2 둘 다).
	// 잠금 · 장착 · 배치 · 인챈트 대기 대상과 마지막 캐릭터는 서버가 남긴다. 되돌릴 수 없어 확인 창을 띄운다.
	[Serializable]
	internal sealed class ClearItemsCheatSection : CheatSection
	{
		private static readonly string[] KindLabels    = { "전체", "자원", "장비", "캐릭터" };
		private static readonly string[] StorageLabels = { "인벤토리", "창고", "둘 다" };

		[SerializeField] private int _kind;
		[SerializeField] private int _storage;

		public override ECheatCategory Category => ECheatCategory.Give;
		public override int            Order    => 99;
		public override string         Title    => "인벤토리 비우기";
		public override Color          Accent   => new(0.85f, 0.35f, 0.35f);   // 빨강 — 지우는 칸
		public override string         Keywords => "clear 삭제 지우기 초기화 창고";
		public override string         Hint     => "잠금 · 장착 · 배치 · 인챈트 대기 대상과 마지막 캐릭터는 남는다\n되돌릴 수 없다";

		public override void Draw()
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("종류");
				_kind = GUILayout.Toolbar(_kind, KindLabels);
			}

			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("보관함");
				_storage = GUILayout.Toolbar(_storage, StorageLabels);
			}

			// 실행 버튼은 고르는 줄과 떼어 아래 오른쪽에 — 토글과 같은 줄이면 선택지 하나로 보인다
			EditorGUILayout.Space(2f);

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();

				var label = $"{StorageLabels[_storage]} · {KindLabels[_kind]} 비우기";

				if (CheatGui.DangerButton(label))
				{
					ConfirmAndSend(_kind, _storage);
				}
			}
		}

		// 확인 창은 OnGUI 밖(다음 틱)에서 띄운다 — 안에서 띄우면 레이아웃 그룹이 어긋난다(규칙 문서 '팝업은 OnGUI 밖에서')
		private static void ConfirmAndSend(int kind, int storage)
		{
			EditorApplication.delayCall += () =>
			{
				if (EditorUtility.DisplayDialog("인벤토리 비우기", $"{StorageLabels[storage]}의 {KindLabels[kind]}을(를) 지웁니다. 되돌릴 수 없습니다.", "비우기", "취소"))
				{
					CheatSender.Send(ECheatCommand.ClearItems, kind, storage);
				}
			};
		}
	}
}
