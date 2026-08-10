using System;
using System.Collections.Generic;
using UnityEngine;

// ── HierarchyPalette : 하이어라키 색칠 규칙을 담는 데이터 ──
//   "이름이 이 문자로 시작하면 이 색으로 칠한다"를 목록으로 들고 있는 ScriptableObject.
//   그리는 일은 짝꿍 HierarchyStyler 가 한다 — 여긴 데이터만 있다.
//
// ■ 왜 접두 문자인가
//   하이어라키에는 폴더가 없다. 오브젝트가 수십 개가 되면 어디까지가 한 덩어리인지 안 보인다.
//   이름 앞에 문자 하나를 붙여 계층을 표시하고, 그 문자를 색으로 바꿔 눈에 띄게 한다.
//   표시할 때 그 문자는 지우므로 하이어라키에는 색만 남는다.
//
// ■ 에셋을 만드는 법
//   Project 창 우클릭 → Create → Arca → Hierarchy Palette.
//   위치는 어디든 상관없다 — HierarchyStyler 가 프로젝트 전체에서 타입으로 찾는다.
//   ※ 팔레트는 하나만 둔다. 여럿이면 먼저 찾힌 것 하나만 쓴다.
[CreateAssetMenu(fileName = "Hierarchy Palette", menuName = "Arca/Hierarchy Palette")]
public class HierarchyPalette : ScriptableObject
{
    /// <summary>접두 문자 하나에 대한 색칠 규칙.</summary>
    [Serializable]
    public class Rule
    {
        [Tooltip("이 문자로 시작하는 오브젝트에 적용한다. 하이어라키에 그릴 때 이 문자는 지운다")]
        public string prefix = "!";

        [Tooltip("글자색 — 알파를 255로 두지 않으면 배경에 묻힌다")]
        public Color textColor = Color.white;

        [Tooltip("줄 전체를 덮을 배경색 — 알파를 255로")]
        public Color backgroundColor = Color.gray;

        public TextAnchor textAlignment = TextAnchor.UpperLeft;
        public FontStyle  fontStyle     = FontStyle.Bold;
    }

    // ※ 위에서부터 훑어 처음 맞는 규칙 하나만 적용한다. 접두 문자가 겹치면 위쪽이 이긴다.
    [Tooltip("위에서부터 검사한다. 한 오브젝트에 처음 맞는 규칙 하나만 적용된다")]
    public List<Rule> rules = new List<Rule>();
}
