using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	// 테이블에서 TID 하나를 고르는 줄 — 검색어로 거른 '이름 (TID)' 목록.
	// 테이블을 아직 읽지 않았으면(플레이 전) TID를 숫자로 직접 받는다.
	[Serializable]
	internal sealed class TidPicker
	{
		public string Filter = "";
		public int    Tid;

		// 검색어·원본이 그대로면 목록을 다시 만들지 않는다 — OnGUI는 이벤트마다 여러 번 불린다.
		[NonSerialized] private string?  _cachedFilter;
		[NonSerialized] private object?  _cachedSource;
		[NonSerialized] private int[]    _tids   = Array.Empty<int>();
		[NonSerialized] private string[] _labels = Array.Empty<string>();

		// 'actionLabel'을 주면 줄 끝에 버튼을 붙이고, 눌렸는지 돌려준다 — 개수 칸이 없는 지급(장비)이 한 줄로 끝난다.
		public bool Draw<TRow>(IReadOnlyList<TRow>? rows, Func<TRow, int> tidOf, Func<TRow, string> nameOf, string? actionLabel = null)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				CheatGui.FieldLabel("TID");

				if (rows == null)
				{
					Tid = EditorGUILayout.IntField(Tid);
					GUILayout.Label("(Play 중 목록)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
				}
				else
				{
					DrawPopup(rows, tidOf, nameOf);
				}

				return actionLabel != null && CheatGui.ActionButton(actionLabel);
			}
		}

		private void DrawPopup<TRow>(IReadOnlyList<TRow> rows, Func<TRow, int> tidOf, Func<TRow, string> nameOf)
		{
			Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(100f));

			if (!ReferenceEquals(rows, _cachedSource) || Filter != _cachedFilter)
			{
				Rebuild(rows, tidOf, nameOf);
			}

			var index = EditorGUILayout.Popup(Array.IndexOf(_tids, Tid), _labels);

			if (index >= 0 && index < _tids.Length)
			{
				Tid = _tids[index];
			}
		}

		private void Rebuild<TRow>(IReadOnlyList<TRow> rows, Func<TRow, int> tidOf, Func<TRow, string> nameOf)
		{
			var tids   = new List<int>();
			var labels = new List<string>();

			foreach (var row in rows)
			{
				var tid   = tidOf(row);
				var label = $"{nameOf(row)} ({tid})";

				if (Filter.Length > 0 && label.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				tids.Add(tid);
				labels.Add(label);
			}

			_tids         = tids.ToArray();
			_labels       = labels.ToArray();
			_cachedFilter = Filter;
			_cachedSource = rows;
		}
	}
}
