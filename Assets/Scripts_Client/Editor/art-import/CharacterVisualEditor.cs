using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 캐릭터 그림 SO의 인스펙터 — 맨 아래에 크기 템플릿 드롭다운(소·중·대)과 크롭 편집('CharacterCropPanel')을 단다.
    //
    // ■ 드롭다운을 고르면 그 템플릿 값으로 개인 조정 세 칸(배율·화면 배율·멈추는 거리)을 채운다
    //   그 뒤 세 칸을 손대면 '커스텀 수치'로 표시되고, [템플릿 수치로 되돌리기]로 돌아간다.
    //   템플릿 값은 이 캐릭터의 그림(대기 첫 프레임·타격 프레임)으로 계산한다 — 'ArtBaker.ComputeSize'.
    //
    // ※ 계산은 그림 파일을 읽어서 무겁다 — 등급이 바뀔 때만 다시 계산하고 그 사이에는 기억해 둔다.
    [CustomEditor(typeof(CharacterVisual))]
    internal sealed class CharacterVisualEditor : Editor
    {
        private static readonly string[] SizeLabels = { "없음", "소형", "중형", "대형" };

        private ECharacterSize          _cachedSize = (ECharacterSize)(-1);
        private CharacterSizeTemplate?  _template;
        private ArtBaker.SizeValues?    _values;
        private CharacterCropPanel?     _crop;

        private void OnEnable()
        {
            _crop = targets.Length == 1 ? new CharacterCropPanel((CharacterVisual)target) : null;
        }

        private void OnDisable()
        {
            _crop?.Dispose();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "sizeClass");
            serializedObject.ApplyModifiedProperties();

            if (targets.Length > 1)
            {
                return; // 여러 개를 함께 고를 때는 템플릿·크롭 칸을 그리지 않는다 — 캐릭터마다 계산값이 다르다
            }

            EditorGUILayout.Space();
            DrawSizeTemplate();

            EditorGUILayout.Space(8);
            _crop?.Draw();
        }

        private void DrawSizeTemplate()
        {
            var                visual = (CharacterVisual)target;
            SerializedProperty size   = serializedObject.FindProperty("sizeClass");

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(new GUIContent("크기 템플릿", "고르면 그 템플릿 값으로 위 개인 조정 세 칸을 채운다"), size.intValue, SizeLabels);

            if (EditorGUI.EndChangeCheck())
            {
                Apply((ECharacterSize)picked);

                return;
            }

            Refresh(visual.SizeClass);

            if (visual.SizeClass == ECharacterSize.None)
            {
                EditorGUILayout.HelpBox("등급 없음 — 다음 굽기가 레시피의 템플릿으로 채운다.", MessageType.None);

                return;
            }

            if (_template == null || _values == null)
            {
                EditorGUILayout.HelpBox($"{SizeLabels[(int)visual.SizeClass]} 템플릿이나 대기 그림이 없다 — 'Assets/Art/characters/_templates/'를 본다.", MessageType.Warning);

                return;
            }

            ArtBaker.SizeValues values = _values.Value;
            float pixel = serializedObject.FindProperty("pixelScaleOverride").floatValue;

            if (values.Matches(visual.CharacterScale, pixel, visual.StopGapOffset))
            {
                EditorGUILayout.HelpBox($"{SizeLabels[(int)visual.SizeClass]} 템플릿 수치 그대로 — {values}", MessageType.Info);

                return;
            }

            EditorGUILayout.HelpBox($"커스텀 수치 적용 중 ({SizeLabels[(int)visual.SizeClass]} 기준)\n템플릿: {values}", MessageType.None);

            if (GUILayout.Button("템플릿 수치로 되돌리기"))
            {
                Apply(visual.SizeClass);
            }
        }

        // 등급을 정하고 그 템플릿 값으로 세 칸을 채운다 — 없음이면 등급만 지운다(세 칸은 그대로)
        private void Apply(ECharacterSize size)
        {
            Refresh(size);

            if (size != ECharacterSize.None && (_template == null || _values == null))
            {
                Debug.LogWarning($"[아트] {target.name}: {SizeLabels[(int)size]} 템플릿이나 대기 그림이 없어 적용하지 못했다");

                return;
            }

            Undo.RecordObject(target, "크기 템플릿 적용");
            serializedObject.Update();

            if (size == ECharacterSize.None)
            {
                serializedObject.FindProperty("sizeClass").intValue = (int)ECharacterSize.None;
            }
            else
            {
                ArtBaker.WriteSize(serializedObject, _template!, _values!.Value);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // 이 등급의 템플릿 값 — 등급이 바뀔 때만 다시 계산한다
        private void Refresh(ECharacterSize size)
        {
            if (size == _cachedSize)
            {
                return;
            }

            _cachedSize = size;
            _template   = ArtBaker.FindSizeTemplate(size);
            _values     = _template != null ? ArtBaker.ComputeSize((CharacterVisual)target, _template) : null;
            ArtImage.ClearCache(); // 그림 읽기 캐시 — 굽기와 달리 한 번 쓰고 버린다
        }
    }
}
