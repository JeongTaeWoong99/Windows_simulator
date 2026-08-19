using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
	/// <summary>
	/// 유니티 내장 에디터 아이콘을 이름으로 가져와 캐시한다(툴바 버튼들이 함께 쓴다).
	///
	/// ★ 'EditorGUIUtility.IconContent'가 아니라 'FindTexture'를 쓴다 —
	///   이름이 틀렸을 때 IconContent는 콘솔에 오류를 뱉지만, FindTexture는 조용히 null을 준다.
	///   아이콘이 없으면 버튼에 텍스트만 나오면 그만이라 조용한 쪽이 맞다.
	/// </summary>
	internal static class EditorIcons
	{
		/// <summary>메모리(램) 아이콘.</summary>
		public const string Memory = "Profiler.Memory";

		/// <summary>복제 아이콘. ★ 'SceneAsset Icon'처럼 **공백이 든 이름은 'FindTexture'가 못 찾는다**(실측).</summary>
		public const string Duplicate = "TreeEditor.Duplicate";

		/// <summary>콘솔 창 아이콘.</summary>
		public const string Console = "UnityEditor.ConsoleWindow";

		private static readonly Dictionary<string, Texture2D?> Cache = new();

		/// <summary>이름으로 내장 아이콘을 얻는다. 없으면 null (툴바 버튼들이 호출).</summary>
		public static Texture2D? Get(string iconName)
		{
			if (Cache.TryGetValue(iconName, out Texture2D? cached) && cached != null)
			{
				return cached;
			}

			// 스킨(밝게/어둡게)이 바뀌면 이전 텍스처가 죽어 '가짜 null'이 된다 — 그때는 다시 찾는다.
			Texture2D? icon = EditorGUIUtility.FindTexture(iconName);
			Cache[iconName] = icon;

			return icon;
		}
	}
}
