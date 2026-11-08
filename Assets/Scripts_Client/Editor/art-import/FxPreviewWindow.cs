using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 번호 붙은 PNG 프레임(<이름>_0.png, <이름>_1.png …)을 에디터에서 이어 재생해 본다.
    //
    // ■ 쓰는 법
    //   메뉴 '아트/이펙트 미리보기'로 창을 열고, Project 창에서 프레임 PNG 하나를 클릭하면 같은 이름의 번호 묶음 전체를 재생한다.
    //   파티클처럼 눌러서 바로 보는 용도 — 게임 코드·목록은 건드리지 않는다.
    internal sealed class FxPreviewWindow : EditorWindow
    {
        private static readonly Regex FramePattern = new(@"^(.+)_(\d+)$");

        private readonly List<Texture2D> _frames = new();

        private string _sequenceName = "";
        private float  _duration     = 0.3f; // 게임의 타격 이펙트 길이(SlotStageView.hitEffectSeconds)와 같은 기본값
        private float  _speed        = 1f;
        private bool   _isPlaying    = true;
        private bool   _isLooping    = true;
        private int    _zoom         = 4;
        private int    _background;          // 0 어둡게 · 1 밝게 · 2 초록(슬롯 배경 비슷)
        private float  _time;
        private double _lastTick;
        private Object? _lastSelected;

        private static readonly Color[] Backgrounds =
        {
            new(0.12f, 0.12f, 0.14f),
            new(0.85f, 0.85f, 0.85f),
            new(0.36f, 0.45f, 0.36f),
        };

        [MenuItem(ArtSpec.MenuRoot + "이펙트 미리보기", priority = 20)]
        private static void Open()
        {
            GetWindow<FxPreviewWindow>("이펙트 미리보기").Show();
        }

        private void OnEnable()
        {
            EditorApplication.update   += Tick;
            _lastTick = EditorApplication.timeSinceStartup;
            OnSelectionChange();
        }

        private void OnDisable()
        {
            EditorApplication.update   -= Tick;
        }

        // 선택한 PNG의 이름에서 번호를 떼고, 같은 폴더의 같은 이름 묶음을 번호 순서로 모은다
        private void OnSelectionChange()
        {
            _lastSelected = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".png"))
            {
                return;
            }

            Match match = FramePattern.Match(Path.GetFileNameWithoutExtension(path));

            if (!match.Success)
            {
                return;
            }

            string prefix = match.Groups[1].Value;
            string folder = Path.GetDirectoryName(path)!.Replace('\\', '/');

            var numbered = new List<(int index, string path)>();

            foreach (string file in Directory.GetFiles(folder, prefix + "_*.png"))
            {
                Match m = FramePattern.Match(Path.GetFileNameWithoutExtension(file));

                if (m.Success && m.Groups[1].Value == prefix)
                {
                    numbered.Add((int.Parse(m.Groups[2].Value), file.Replace('\\', '/')));
                }
            }

            _frames.Clear();
            _frames.AddRange(numbered.OrderBy(f => f.index)
                                     .Select(f => AssetDatabase.LoadAssetAtPath<Texture2D>(f.path))
                                     .Where(t => t != null));

            _sequenceName = prefix;
            _time         = 0f;
            _isPlaying    = true;
            Repaint();
        }

        private void Tick()
        {
            double now   = EditorApplication.timeSinceStartup;
            float  delta = (float)(now - _lastTick);
            _lastTick = now;

            // 선택 알림이 빠지는 경우(코드로 선택을 바꿀 때 등)를 대비해 직접 비교한다
            if (Selection.activeObject != _lastSelected)
            {
                OnSelectionChange();
            }

            if (!_isPlaying || _frames.Count == 0)
            {
                return;
            }

            _time += delta * _speed;

            if (_time >= _duration)
            {
                if (_isLooping)
                {
                    // 한 번 끝나면 잠깐 비웠다가 다시 — 게임에서 타격이 한 번씩 터지는 느낌
                    _time = _time >= _duration + 0.25f ? 0f : _time;
                }
                else
                {
                    _time      = _duration;
                    _isPlaying = false;
                }
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (_frames.Count == 0)
            {
                EditorGUILayout.HelpBox("Project 창에서 번호 붙은 프레임 PNG(예: fx/hit/hit_mining_0.png)를 하나 클릭하세요.", MessageType.Info);
                return;
            }

            DrawControls();

            int frame = CurrentFrame();
            Rect area = GUILayoutUtility.GetRect(position.width, position.height - 110f);
            EditorGUI.DrawRect(area, Backgrounds[_background]);

            if (frame >= 0)
            {
                Texture2D tex  = _frames[frame];
                var       size = new Vector2(tex.width, tex.height) * _zoom;
                var       rect = new Rect(area.center - size / 2f, size);
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            }

            EditorGUILayout.LabelField(frame >= 0
                ? $"{_sequenceName}  ·  {frame + 1} / {_frames.Count}장  ·  {_frames[0].width}×{_frames[0].height}"
                : $"{_sequenceName}  ·  (쉬는 중)");
        }

        private void DrawControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_isPlaying ? "❚❚ 멈춤" : "▶ 재생", GUILayout.Width(70)))
                {
                    if (!_isPlaying && _time >= _duration)
                    {
                        _time = 0f;
                    }

                    _isPlaying = !_isPlaying;
                }

                _isLooping  = GUILayout.Toggle(_isLooping, "반복", GUILayout.Width(50));
                _background = GUILayout.Toolbar(_background, new[] { "어둡게", "밝게", "초록" });
            }

            _duration = EditorGUILayout.Slider("길이(초)", _duration, 0.1f, 2f);
            _speed    = EditorGUILayout.Slider("속도", _speed, 0.1f, 2f);
            _zoom     = EditorGUILayout.IntSlider("확대", _zoom, 1, 8);

            // 멈춘 채로 한 장씩 넘겨 보기
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.IntSlider("프레임", Mathf.Max(0, CurrentFrame()), 0, _frames.Count - 1);

            if (EditorGUI.EndChangeCheck())
            {
                _isPlaying = false;
                _time      = (picked + 0.5f) * _duration / _frames.Count;
            }
        }

        // 게임과 같은 규칙 — 길이를 장 수로 똑같이 나눈다. 길이를 넘긴 쉬는 구간은 -1
        private int CurrentFrame()
        {
            if (_time >= _duration)
            {
                return _isLooping ? -1 : _frames.Count - 1;
            }

            return Mathf.Clamp((int)(_time / _duration * _frames.Count), 0, _frames.Count - 1);
        }
    }
}
