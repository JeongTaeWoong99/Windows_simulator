using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesktopWindowControl.EditorTools
{
    // 잘린 글자 검사기 — 지금 화면에 켜진 TMP 글자 중 칸을 넘거나 '…'로 잘린 것을 화면(최상위 캔버스)별로 뽑는다 (T-201).
    //
    // ■ 무엇을 '문제'로 보나
    //   잘림 — TMP가 글자를 잘라냈다('isTextTruncated' · Ellipsis/Truncate).
    //   넘침 — 그려진 글자가 칸 밖으로 나갔다(Overflow 모드 · 가로/세로 1.5px 넘게).
    //   ※ 자동 축소가 최소 크기에 닿았는데도 안 들어가면 위 둘 중 하나로 잡힌다.
    //
    // ■ 플레이 중에 돌린다 — 글자는 실제 값(이름·숫자)이 채워져야 길이가 맞다. 편집 모드에서는 견본 글자를 잰다.
    // ■ 켜진 것만 본다 — 꺼진 창은 그 창을 열고 다시 돌린다. 투명(CanvasGroup alpha 0)한 창도 건너뛴다.
    internal static class TextOverflowChecker
    {
        private const string MenuPath = "Window/DesktopWindowControl/UI/잘린 글자 검사";

        // 이만큼은 반올림 오차로 본다 (px)
        private const float Tolerance = 1.5f;

        // 자동 축소 글자는 이만큼 더 봐준다 (px) — TMP는 글자 '진행 폭'으로 맞추고, 그려진 모양은 끝 글자 획이
        // 1~2px 삐져나온다(금화 '80,136,504' 152.6 > 151 · 2026-11-15). 눈에 안 보이는 차이라 문제로 세지 않는다.
        private const float AutoSizeTolerance = 3f;

        [MenuItem(MenuPath)]
        private static void RunFromMenu()
        {
            string report = Run();

            Debug.Log(report);
        }

        // 검사해 보고서 문자열을 돌려준다 (메뉴 · 자동화 eval에서 호출).
        public static string Run()
        {
            var problems = new SortedDictionary<string, List<string>>();
            int checkedCount = 0;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(false))
                    {
                        if (!IsVisible(text))
                        {
                            continue;
                        }

                        checkedCount++;

                        string? issue = Inspect(text);

                        if (issue == null)
                        {
                            continue;
                        }

                        string screen = ScreenOf(text.transform);

                        if (!problems.TryGetValue(screen, out List<string> list))
                        {
                            list = new List<string>();
                            problems.Add(screen, list);
                        }

                        list.Add($"  {issue} · {PathOf(text.transform)} · \"{Shorten(text.text)}\"");
                    }
                }
            }

            int total = 0;

            foreach (List<string> list in problems.Values)
            {
                total += list.Count;
            }

            var sb = new StringBuilder();

            sb.AppendLine($"잘린 글자 검사 — 켜진 글자 {checkedCount}개 중 문제 {total}개");

            foreach (KeyValuePair<string, List<string>> pair in problems)
            {
                sb.AppendLine($"[{pair.Key}] {pair.Value.Count}개");

                foreach (string line in pair.Value)
                {
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        // 글자 하나를 잰다 — 문제가 없으면 null
        private static string? Inspect(TMP_Text text)
        {
            if (string.IsNullOrEmpty(text.text))
            {
                return null;
            }

            text.ForceMeshUpdate(true, true);

            if (text.isTextTruncated)
            {
                return "잘림";
            }

            if (text.textInfo.characterCount == 0)
            {
                return null;
            }

            Rect    rect     = text.rectTransform.rect;
            Vector2 rendered = text.GetRenderedValues(true);

            float tolerance = text.enableAutoSizing ? AutoSizeTolerance : Tolerance;

            bool wide = rendered.x > rect.width + tolerance;
            bool tall = rendered.y > rect.height + tolerance;

            if (wide || tall)
            {
                return $"넘침({(wide ? $"가로 {rendered.x:0}>{rect.width:0}" : "")}{(wide && tall ? " · " : "")}{(tall ? $"세로 {rendered.y:0}>{rect.height:0}" : "")})";
            }

            return null;
        }

        // 화면에 보이나 — 켜져 있고, 위쪽 CanvasGroup이 투명하지 않고, 캔버스가 켜져 있다
        private static bool IsVisible(TMP_Text text)
        {
            if (!text.isActiveAndEnabled || text.color.a <= 0.01f)
            {
                return false;
            }

            foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>())
            {
                if (group.alpha <= 0.01f)
                {
                    return false;
                }

                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            Canvas canvas = text.canvas;

            return canvas != null && canvas.enabled;
        }

        // 화면 이름 — 위로 올라가며 처음 만나는 '#'(열)·'!'(오버레이) 이름표 캔버스. 없으면 루트 이름
        private static string ScreenOf(Transform t)
        {
            for (Transform? cur = t; cur != null; cur = cur.parent)
            {
                if ((cur.name.StartsWith("#") || cur.name.StartsWith("!")) && cur.GetComponent<Canvas>() != null)
                {
                    return cur.name;
                }
            }

            return t.root.name;
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();

            for (Transform? cur = t; cur != null && parts.Count < 4; cur = cur.parent)
            {
                parts.Insert(0, cur.name);
            }

            return string.Join("/", parts);
        }

        private static string Shorten(string value)
        {
            string one = value.Replace("\n", " ");

            return one.Length > 30 ? one.Substring(0, 30) + "…" : one;
        }
    }
}
