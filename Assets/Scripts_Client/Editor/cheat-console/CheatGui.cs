using System.Collections.Generic;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 치트 칸들이 함께 쓰는 그리기 부품·보내기 창구. 줄 격자(라벨 | 입력 | 버튼)를 맞추는 폭도 여기 한 곳에 둔다.
	internal static class CheatGui
	{
		public const float LabelWidth         = 64f;
		public const float ButtonWidth        = 60f;
		public const float RowIndent          = 10f;   // 칸 안 줄 들여쓰기(px) — 묶음 띠와 줄이 같은 선에서 시작한다
		public const float StatusWidth        = 42f;   // 해금 줄의 '[열림]'·'[잠김]' 고정 폭 — 이름 열을 맞춘다
		public const float GroupButtonWidth   = 96f;   // 묶음 띠 오른쪽 끝 일괄 버튼 폭(px)

		private const float AccentWidth     = 4f;    // 칸 왼쪽 색 띠 두께(px)
		private const float BandHeight      = 18f;   // 칸 안 묶음 띠 높이(px)
		private const float HeaderIconSize  = 14f;

		private const string InfoIconName = "console.infoicon.sml";
		private const string WarnIconName = "console.warnicon.sml";

		public static readonly Color DoneColor       = new(0.45f, 0.85f, 0.45f);
		public static readonly Color PendingColor    = new(0.95f, 0.65f, 0.25f);
		public static readonly Color ErrorColor      = new(1.00f, 0.45f, 0.45f);
		public static readonly Color ServerWideColor = new(1.00f, 0.60f, 0.30f);

		private static Texture2D? WarnIcon => EditorIcons.Get(WarnIconName);

		#region 보내기

		// 치트를 다음 에디터 틱에 보낸다 (칸마다의 버튼)
		// ★ 버튼 처리 중(OnGUI 안)에 모달 팝업을 띄우면 레이아웃 그룹이 어긋나 오류가 난다 — 'CheatGuard' 팝업을 OnGUI 밖으로 뺀다.
		public static void Request(ECheatCommand command, long arg1 = 0, long arg2 = 0)
		{
			EditorApplication.delayCall += () => CheatSender.Send(command, arg1, arg2);
		}

		// 같은 명령 여러 개를 한 번에 보낸다 (해금 전부 열기 · 산업 최대).
		//
		// ⚠️ 가드를 **먼저 한 번만** 본다 — 'CheatSender.Send'마다 보게 두면 로그인 전에 누른 순간
		//    경고 팝업이 줄 수만큼 뜬다.
		public static void RequestAll(ECheatCommand command, List<(long Arg1, long Arg2)> args)
		{
			EditorApplication.delayCall += () =>
			{
				if (!CheatGuard.CanSend())
				{
					return;
				}

				foreach (var (arg1, arg2) in args)
				{
					CheatSender.Send(command, arg1, arg2);
				}
			};
		}

		#endregion

		#region 줄 부품

		// 줄 맨 앞 고정 폭 라벨 — 모든 칸의 입력 열이 같은 선에서 시작한다.
		public static void FieldLabel(string label)
		{
			EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
		}

		// 줄 맨 끝 고정 폭 버튼 — 모든 칸의 버튼 열이 같은 선에 선다.
		public static bool ActionButton(string label)
		{
			return GUILayout.Button(label, GUILayout.Width(ButtonWidth));
		}

		// 색 입힌 한 줄. 'width'를 주면 고정 폭으로 그린다 — 여러 줄의 다음 칸을 세로로 맞출 때 쓴다.
		public static void DrawColoredLabel(string text, Color color, float width = 0f)
		{
			var previous = GUI.contentColor;
			GUI.contentColor = color;

			if (width > 0f)
			{
				GUILayout.Label(text, EditorStyles.miniLabel, GUILayout.Width(width));
			}
			else
			{
				GUILayout.Label(text, EditorStyles.miniLabel);
			}

			GUI.contentColor = previous;
		}

		// 칸 안을 가르는 묶음 띠 — 칸 머리('BeginSection')와 같은 언어를 더 얇게 쓴다. 띠 영역을 돌려준다(오른쪽 끝 버튼 자리).
		// 옅은 바탕 + 아래 실선 한 줄. 이름만 덩그러니 띄우면 어느 쪽에 붙는 줄인지 안 보인다.
		//
		//   'isFollowing' : 앞에 다른 띠가 있었으면 위에 숨을 한 번 준다(첫 띠는 칸 머리에 바로 붙인다).
		//
		// ※ 'EditorGUI.IndentLevelScope'를 쓰지 않는다 — indentLevel은 'GUILayout' 줄에 먹지 않아
		//   띠만 밀리고 줄은 제자리에 남는다. 둘 다 'RowIndent'로 직접 민다.
		public static Rect DrawBand(string name, bool isFollowing, Color accent)
		{
			if (isFollowing)
			{
				EditorGUILayout.Space(4f);
			}

			var head = EditorGUILayout.GetControlRect(false, BandHeight);

			head.x     += RowIndent;
			head.width -= RowIndent;

			EditorGUI.DrawRect(head, new Color(accent.r, accent.g, accent.b, 0.10f));
			EditorGUI.DrawRect(new Rect(head.x, head.yMax - 1f, head.width, 1f), new Color(accent.r, accent.g, accent.b, 0.45f));

			EditorGUI.LabelField(new Rect(head.x + 4f, head.y, head.width - 4f, head.height), name, EditorStyles.miniBoldLabel);

			return head;
		}

		#endregion

		#region 칸 머리 · 상자

		// 칸 머리 — 색 띠 + 옅게 물든 바탕 + 굵은 제목, 오른쪽 끝에 '서버 전체' 배지와 ⓘ 툴팁. 본문은 'EndSection'까지 상자 안에 그린다.
		public static void BeginSection(CheatSection section)
		{
			EditorGUILayout.Space(6f);

			var accent = section.Accent;
			var header = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 2f);
			EditorGUI.DrawRect(header, new Color(accent.r, accent.g, accent.b, 0.22f));
			EditorGUI.DrawRect(new Rect(header.x, header.y, AccentWidth, header.height), accent);

			var right = header.xMax - 4f;

			if (section.Hint.Length > 0)
			{
				right = DrawHint(header, right, section.Hint);
			}

			if (section.IsServerWide)
			{
				right = DrawServerWideBadge(header, right);
			}

			var titleRect = new Rect(header.x + AccentWidth + 6f, header.y, right - header.x - AccentWidth - 6f, header.height);
			EditorGUI.LabelField(titleRect, section.Title, EditorStyles.boldLabel);

			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		}

		// 칸 본문을 닫고, 본문 상자 왼쪽에도 같은 색 띠를 그어 머리와 이어 보이게 한다.
		// 상자 크기는 레이아웃이 끝난 뒤에야 알 수 있어 Repaint 때만 그린다.
		public static void EndSection(Color accent)
		{
			EditorGUILayout.EndVertical();

			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			var body = GUILayoutUtility.GetLastRect();
			EditorGUI.DrawRect(new Rect(body.x, body.y, AccentWidth, body.height), accent);
		}

		// ⓘ — 마우스를 올리면 안내문이 뜬다. 아이콘을 못 찾으면 '?' 글자로 대신한다. 남은 오른쪽 끝을 돌려준다.
		private static float DrawHint(Rect header, float right, string hint)
		{
			var icon    = EditorIcons.Get(InfoIconName);
			var content = icon != null ? new GUIContent(icon, hint) : new GUIContent("?", hint);
			var rect    = new Rect(right - HeaderIconSize, header.center.y - HeaderIconSize / 2f, HeaderIconSize, HeaderIconSize);

			GUI.Label(rect, content, icon != null ? GUIStyle.none : EditorStyles.miniBoldLabel);

			return rect.x - 6f;
		}

		// '서버 전체' 배지 — 접속 중인 모두에게 걸리는 치트를 내 계정 치트와 눈으로 가른다. 남은 오른쪽 끝을 돌려준다.
		private static float DrawServerWideBadge(Rect header, float right)
		{
			var content = new GUIContent(" 서버 전체", WarnIcon, "접속 중인 모두에게 걸린다");

			using (new EditorGUIUtility.IconSizeScope(new Vector2(HeaderIconSize - 2f, HeaderIconSize - 2f)))
			{
				var width = EditorStyles.miniBoldLabel.CalcSize(content).x;
				var rect  = new Rect(right - width, header.y, width, header.height);

				var previous = GUI.contentColor;
				GUI.contentColor = ServerWideColor;
				GUI.Label(rect, content, EditorStyles.miniBoldLabel);
				GUI.contentColor = previous;

				return rect.x - 6f;
			}
		}

		#endregion
	}
}
