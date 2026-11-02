using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 캐릭터 SO 인스펙터의 크롭 편집 — 인벤토리 상반신·위젯 머리를 그림 위에서 끌어 고르고, 화면에 보일 모양을 전·후로 비교한다.
    //
    // ■ 왜 SO가 아니라 레시피에 쓰나
    //   크롭 사각형은 레시피의 'portraitRect'·'headRect'(결과 칸 좌표)이고, 굽기가 그 사각형으로 그림 파일을 만든다.
    //   [적용]은 레시피에 쓰고 이 캐릭터 하나만 다시 굽는다 — SO의 상반신·머리 참조는 같은 파일이라 그대로 산다.
    //   자동 크롭과 같은 사각형이면 0으로 저장해 계속 자동을 따르게 한다(템플릿·그림이 바뀌면 같이 움직인다).
    //
    // ■ 조작
    //   상자 안을 끌면 옮긴다 · 오른쪽 위 모서리를 끌면 크기(정사각형, 왼쪽 아래 고정) · 휠은 가운데 기준 크기 ±1.
    //   좌표는 결과 칸(224×96)의 아트 픽셀이다 — 왼쪽 아래 (0,0) · 발은 x=112.
    internal sealed class CharacterCropPanel
    {
        private enum Target
        {
            Portrait,
            Head,
        }

        private static readonly string[] TargetLabels = { "인벤토리 상반신", "위젯 머리" };
        private static readonly Color    PortraitColor = new(1f, 0.6f, 0.1f);
        private static readonly Color    HeadColor     = new(0.3f, 1f, 0.4f);
        private static readonly Color    CardColor     = new Color32(0x1F, 0x30, 0x44, 0xFF);

        // 게임 화면의 실제 크기(UI 기준 해상도 픽셀) — 인벤토리 칸 · 배치 카드 · 위젯 칸 머리('WidgetMiniSlotView'는 늘려 채운다)
        private const float InventorySlot = 64f;
        private const float AssignedCard  = 80f;
        private static readonly Vector2 WidgetHead = new(43f, 40f);

        private const float HandleSize = 10f;

        // 보는 창 — 캐릭터 둘레만 크게 본다(224×96 전체를 그리면 캐릭터가 작아 상자를 고르기 힘들다)
        private const int   ViewPadding   = 6;
        private const int   ViewMinSize   = 40;
        private const float ViewMaxHeight = 320f;

        private readonly CharacterVisual _visual;
        private CharacterRecipe?         _recipe;
        private ArtBaker.CropSource?     _source;
        private Texture2D?               _cellTexture;
        private Texture2D?               _portraitAfter;
        private Texture2D?               _headAfter;

        private RectInt _portrait;
        private RectInt _head;
        private RectInt _savedPortrait;
        private RectInt _savedHead;
        private RectInt _view;
        private Target  _target;
        private bool    _open = true;

        // 끌기 상태 — 0 없음 · 1 옮기기 · 2 크기
        private int     _drag;
        private Vector2 _dragStart;
        private RectInt _dragFrom;

        public CharacterCropPanel(CharacterVisual visual)
        {
            _visual = visual;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_cellTexture);
            Object.DestroyImmediate(_portraitAfter);
            Object.DestroyImmediate(_headAfter);
        }

        public void Draw()
        {
            _open = EditorGUILayout.Foldout(_open, "크롭 편집 — 인벤토리 상반신 · 위젯 머리", true, EditorStyles.foldoutHeader);

            if (!_open)
            {
                return;
            }

            if (_source == null && !Load())
            {
                EditorGUILayout.HelpBox("레시피나 대기·달리기 원본을 찾지 못했다 — 이 SO 옆에 '<키>_recipe.asset'이 있어야 한다.", MessageType.Warning);

                return;
            }

            ArtBaker.CropSource source = _source!.Value;

            _target = (Target)GUILayout.Toolbar((int)_target, TargetLabels);
            DrawCanvas(source);
            DrawFields();
            EditorGUILayout.Space(4);
            DrawPreviews();
            EditorGUILayout.Space(4);
            DrawButtons(source);
        }

        #region 불러오기

        private bool Load()
        {
            _recipe = ArtBaker.FindRecipe(_visual);

            if (_recipe == null)
            {
                return false;
            }

            _source = ArtBaker.LoadCropSource(_recipe, _visual.SizeClass);
            ArtImage.ClearCache();

            if (_source == null)
            {
                return false;
            }

            ArtBaker.CropSource source = _source.Value;
            Object.DestroyImmediate(_cellTexture);
            _cellTexture = ToTexture(source.Cell);

            _savedPortrait = _recipe.PortraitRect.width > 0 ? _recipe.PortraitRect : source.AutoPortrait;
            _savedHead     = _recipe.HeadRect.width > 0     ? _recipe.HeadRect     : source.AutoHead;
            _portrait      = _savedPortrait;
            _head          = _savedHead;
            _view          = ViewAround(source.Cell.OpaqueBounds(), source.AutoPortrait, source.AutoHead, _savedPortrait, _savedHead);
            RebuildAfter();

            return true;
        }

        private void RebuildAfter()
        {
            ArtImage cell = _source!.Value.Cell;
            Object.DestroyImmediate(_portraitAfter);
            Object.DestroyImmediate(_headAfter);
            _portraitAfter = ToTexture(ArtBaker.CropResult(cell, _portrait, head: false));
            _headAfter     = ToTexture(ArtBaker.CropResult(cell, _head,     head: true));
        }

        private static Texture2D ToTexture(ArtImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
                hideFlags  = HideFlags.HideAndDontSave,
            };

            texture.SetPixels32(image.Pixels);
            texture.Apply();

            return texture;
        }

        #endregion

        #region 그림 위 상자

        // 캐릭터 몸과 크롭 상자들을 모두 담는 창 — 여백을 두고 너무 작지 않게
        private static RectInt ViewAround(params RectInt[] rects)
        {
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;

            foreach (RectInt r in rects)
            {
                if (r.width <= 0)
                {
                    continue;
                }

                xMin = Mathf.Min(xMin, r.xMin);
                yMin = Mathf.Min(yMin, r.yMin);
                xMax = Mathf.Max(xMax, r.xMax);
                yMax = Mathf.Max(yMax, r.yMax);
            }

            var view = new RectInt(xMin - ViewPadding, yMin - ViewPadding, xMax - xMin + ViewPadding * 2, yMax - yMin + ViewPadding * 2);
            int grow = Mathf.Max(0, ViewMinSize - view.width);
            view.xMin -= grow / 2;
            view.width += grow;
            grow = Mathf.Max(0, ViewMinSize - view.height);
            view.yMin -= grow / 2;
            view.height += grow;

            return view;
        }

        private void DrawCanvas(ArtBaker.CropSource source)
        {
            Vector2Int cell  = ArtSpec.CharacterCell;
            float      width = EditorGUIUtility.currentViewWidth - 40f;
            float      scale = Mathf.Max(2f, Mathf.Floor(Mathf.Min(width / _view.width, ViewMaxHeight / _view.height)));
            Rect       outer = GUILayoutUtility.GetRect(_view.width * scale, _view.height * scale, GUILayout.ExpandWidth(false));

            // 창 밖으로 나간 상자·손잡이가 인스펙터 위에 그려지지 않게 자른다 — 안쪽 좌표는 (0,0)부터
            GUI.BeginClip(outer);
            var area = new Rect(0f, 0f, outer.width, outer.height);

            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.14f));

            // 그림은 칸 안에 걸친 부분만 — 창이 칸 밖(발 아래 등)으로 나가면 그 자리는 바탕색
            var inside = new RectInt(Mathf.Max(_view.xMin, 0), Mathf.Max(_view.yMin, 0), 0, 0);
            inside.xMax = Mathf.Min(_view.xMax, cell.x);
            inside.yMax = Mathf.Min(_view.yMax, cell.y);

            if (inside.width > 0 && inside.height > 0)
            {
                var uv = new Rect((float)inside.x / cell.x, (float)inside.y / cell.y, (float)inside.width / cell.x, (float)inside.height / cell.y);
                GUI.DrawTextureWithTexCoords(ToGui(area, scale, inside), _cellTexture, uv);
            }

            // 발 위치(가운데 세로줄)
            EditorGUI.DrawRect(new Rect((cell.x / 2f - _view.x) * scale, 0f, 1f, area.height), new Color(1f, 1f, 1f, 0.15f));

            DrawBox(ToGui(area, scale, _portrait), PortraitColor, _target == Target.Portrait);
            DrawBox(ToGui(area, scale, _head),     HeadColor,     _target == Target.Head);

            HandleMouse(area, scale);
            GUI.EndClip();
        }

        // 칸 좌표(왼쪽 아래 원점) → 보는 창 안의 화면 사각형(왼쪽 위 원점)
        private Rect ToGui(Rect area, float scale, RectInt rect)
            => new(area.x + (rect.x - _view.x) * scale, area.y + (_view.yMax - rect.yMax) * scale, rect.width * scale, rect.height * scale);

        private static void DrawBox(Rect r, Color color, bool active)
        {
            float t = active ? 2f : 1f;
            Color c = active ? color : new Color(color.r, color.g, color.b, 0.45f);

            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);

            if (active)
            {
                EditorGUI.DrawRect(new Rect(r.xMax - HandleSize / 2f, r.y - HandleSize / 2f, HandleSize, HandleSize), c);
            }
        }

        private void HandleMouse(Rect area, float scale)
        {
            Event   e      = Event.current;
            RectInt active = _target == Target.Portrait ? _portrait : _head;
            Rect    box    = ToGui(area, scale, active);
            var     handle = new Rect(box.xMax - HandleSize, box.y - HandleSize, HandleSize * 2f, HandleSize * 2f);

            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeUpRight);
            EditorGUIUtility.AddCursorRect(box, MouseCursor.MoveArrow);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && area.Contains(e.mousePosition):
                    _drag      = handle.Contains(e.mousePosition) ? 2 : box.Contains(e.mousePosition) ? 1 : 0;
                    _dragStart = e.mousePosition;
                    _dragFrom  = active;

                    if (_drag != 0)
                    {
                        e.Use();
                    }

                    break;

                case EventType.MouseDrag when _drag != 0:
                    Vector2 delta = (e.mousePosition - _dragStart) / scale;
                    int     dx    = Mathf.RoundToInt(delta.x);
                    int     dy    = -Mathf.RoundToInt(delta.y);

                    SetActive(_drag == 1
                        ? new RectInt(_dragFrom.x + dx, _dragFrom.y + dy, _dragFrom.width, _dragFrom.height)
                        : Square(_dragFrom.x, _dragFrom.y, _dragFrom.width + Mathf.Max(dx, dy)));
                    e.Use();

                    break;

                case EventType.MouseUp when _drag != 0:
                    _drag = 0;
                    e.Use();

                    break;

                case EventType.ScrollWheel when area.Contains(e.mousePosition):
                    int step = e.delta.y > 0 ? -1 : 1;
                    SetActive(Square(active.x, active.y, active.width + step * 2, centerOf: active));
                    e.Use();

                    break;
            }
        }

        // 정사각형 — 'centerOf'가 있으면 그 가운데를 지킨다
        private static RectInt Square(int x, int y, int size, RectInt? centerOf = null)
        {
            size = Mathf.Clamp(size, 4, ArtSpec.CharacterCell.y);

            if (centerOf is RectInt c)
            {
                x = c.x + (c.width - size) / 2;
                y = c.y + (c.height - size) / 2;
            }

            return new RectInt(x, y, size, size);
        }

        // 상자의 절반까지만 칸 밖으로 나가게 한다 — 작은 캐릭터는 자동 크롭도 발 아래로 조금 나간다(빈 곳은 투명)
        private static RectInt Clamp(RectInt rect)
        {
            Vector2Int cell = ArtSpec.CharacterCell;
            int        size = Mathf.Clamp(rect.width, 4, cell.y);
            int        half = size / 2;

            return new RectInt(Mathf.Clamp(rect.x, -half, cell.x - half), Mathf.Clamp(rect.y, -half, cell.y - half), size, size);
        }

        private void SetActive(RectInt rect)
        {
            rect = Clamp(rect);

            if (_target == Target.Portrait)
            {
                if (rect.Equals(_portrait))
                {
                    return;
                }

                _portrait = rect;
            }
            else
            {
                if (rect.Equals(_head))
                {
                    return;
                }

                _head = rect;
            }

            RebuildAfter();
            GUI.changed = true;
        }

        private void DrawFields()
        {
            RectInt active = _target == Target.Portrait ? _portrait : _head;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 34f;
                int x    = EditorGUILayout.IntField("X",    active.x);
                int y    = EditorGUILayout.IntField("Y",    active.y);
                int size = EditorGUILayout.IntField("크기", active.width);
                EditorGUIUtility.labelWidth = 0f;

                if (x != active.x || y != active.y || size != active.width)
                {
                    SetActive(new RectInt(x, y, size, size));
                }
            }

            EditorGUILayout.LabelField("상자 안을 끌면 옮기기 · 오른쪽 위 모서리를 끌면 크기 · 휠은 가운데 기준 크기", EditorStyles.miniLabel);
        }

        #endregion

        #region 전·후 미리보기

        private void DrawPreviews()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPreviewColumn("현재 (구운 그림)", _visual.Portrait, null, _visual.Head, null);
                GUILayout.Space(16);
                DrawPreviewColumn("변경 후", null, _portraitAfter, null, _headAfter);
            }
        }

        private static void DrawPreviewColumn(string title, Sprite? portrait, Texture2D? portraitTexture, Sprite? head, Texture2D? headTexture)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(AssignedCard + InventorySlot + WidgetHead.x + 24f)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    Slot("인벤토리", new Vector2(InventorySlot, InventorySlot), portrait, portraitTexture, keepAspect: true);
                    Slot("배치 카드", new Vector2(AssignedCard, AssignedCard), portrait, portraitTexture, keepAspect: true);
                    Slot("위젯", WidgetHead, head, headTexture, keepAspect: false);
                }
            }
        }

        // 게임 칸 크기 그대로 한 장 — 인벤토리는 비율을 지켜 채우고, 위젯은 늘려 채운다(게임과 같다)
        private static void Slot(string label, Vector2 size, Sprite? sprite, Texture2D? texture, bool keepAspect)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(size.x + 4f)))
            {
                Rect r = GUILayoutUtility.GetRect(size.x, size.y, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(r, CardColor);

                ScaleMode mode = keepAspect ? ScaleMode.ScaleToFit : ScaleMode.StretchToFill;

                if (texture != null)
                {
                    GUI.DrawTexture(r, texture, mode);
                }
                else if (sprite != null)
                {
                    Rect   t  = sprite.textureRect;
                    var    uv = new Rect(t.x / sprite.texture.width, t.y / sprite.texture.height, t.width / sprite.texture.width, t.height / sprite.texture.height);
                    Rect   to = keepAspect ? Fit(r, t.width / t.height) : r;
                    GUI.DrawTextureWithTexCoords(to, sprite.texture, uv);
                }

                EditorGUILayout.LabelField(label, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(size.x));
            }
        }

        private static Rect Fit(Rect r, float aspect)
        {
            if (r.width / r.height > aspect)
            {
                float w = r.height * aspect;

                return new Rect(r.x + (r.width - w) / 2f, r.y, w, r.height);
            }

            float h = r.width / aspect;

            return new Rect(r.x, r.y + (r.height - h) / 2f, r.width, h);
        }

        #endregion

        #region 버튼

        private void DrawButtons(ArtBaker.CropSource source)
        {
            bool dirty = !_portrait.Equals(_savedPortrait) || !_head.Equals(_savedHead);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("자동 크롭으로"))
                {
                    if (_target == Target.Portrait)
                    {
                        SetActive(source.AutoPortrait);
                    }
                    else
                    {
                        SetActive(source.AutoHead);
                    }
                }

                using (new EditorGUI.DisabledScope(!dirty))
                {
                    if (GUILayout.Button("되돌리기"))
                    {
                        _portrait = _savedPortrait;
                        _head     = _savedHead;
                        RebuildAfter();
                    }

                    GUI.backgroundColor = dirty ? new Color(0.5f, 0.9f, 1f) : Color.white;

                    if (GUILayout.Button("적용 · 이 캐릭터 다시 굽기"))
                    {
                        Apply(source);
                    }

                    GUI.backgroundColor = Color.white;
                }
            }

            if (dirty)
            {
                EditorGUILayout.HelpBox("아직 적용하지 않았다 — 게임은 '현재' 그림 그대로다.", MessageType.None);
            }
        }

        // 레시피에 쓰고 이 캐릭터만 다시 굽는다. 자동과 같으면 0(자동 따름)으로 둔다
        private void Apply(ArtBaker.CropSource source)
        {
            var so = new SerializedObject(_recipe!);
            Undo.RecordObject(_recipe!, "크롭 편집");
            so.FindProperty("portraitRect").rectIntValue = _portrait.Equals(source.AutoPortrait) ? default : _portrait;
            so.FindProperty("headRect").rectIntValue     = _head.Equals(source.AutoHead)         ? default : _head;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            string log = ArtBaker.Bake(_recipe!);
            ArtImage.ClearCache();
            AssetDatabase.SaveAssets();
            Debug.Log($"[아트] 크롭 적용 — {log}");

            _savedPortrait = _portrait;
            _savedHead     = _head;
        }

        #endregion
    }
}
