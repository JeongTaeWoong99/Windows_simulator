using System.Collections.Generic;
using GameData;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터가 낀 장비 4칸을 작은 네모 4개로 — 빈 칸은 어두운 네모, 낀 칸은 장비 등급색 (+ 아이콘) (T-104).
//
// ■ 장비의 능력치 칸과 같은 문법이다
// 네모 크기·간격·테두리·빈 색('RarityPalette.EmptySocket')이 장비 칸의 능력치 칸 줄과 같다 — 새로 배울 표시가 없다.
// 다른 점은 개수다: 능력치 칸은 1~3개가 오른쪽부터 차고, 이 줄은 **언제나 4자리 고정**이다(빈 칸도 자리를 지킨다).
//
// ■ 두 모양 — 같은 스크립트, 프리팹 둘
// 'EquipPipsView'   등급색 네모만 — 인벤토리 캐릭터 칸('SlotView'). 100px 칸이라 아이콘이 읽히지 않는다.
//                   LV 배지와 같은 줄, 오른쪽 끝은 적성 스트립 끝과 맞춘다.
// 'EquipIconsView'  등급 바탕 + 장비 아이콘 — 작업슬롯 칸('WorkStationSlotView') 남은 시간 줄의 오른쪽.
//                   칸이 넓어 아이콘을 띄울 자리가 있다(2026-10-07 사용자 결정). 네모는 줄 높이만큼 정사각형('SquareLayoutElement').
// 슬롯 설정의 캐릭터 카드에는 두지 않는다 — 그 화면은 장비 칸 4개가 바로 옆에 보인다(2026-10-07 결정).
//
// 값은 부르는 쪽이 'EquipLabel.ReadWornGrades'로 완성해 넘긴다 — 이 줄은 캐릭터도 장비도 모른다.
public class EquipPipsView : MonoBehaviour
{
    // ⚠️ **배열 순서 = 'EquipLabel.WornSlots' = 화면의 왼쪽 → 오른쪽**(무기 · 장신구1 · 장신구2 · 보석).
    //   인스펙터에서 순서를 섞으면 무기 등급이 보석 자리에 조용히 들어간다.
    [SerializeField, NonReorderable, Tooltip("장착 네모 4개(등급 바탕). 순서 = 무기 · 장신구1 · 장신구2 · 보석")]
    private Image[] pipImages = new Image[0];

    // 비워 두면 등급색 네모만 그린다. 채우면 'pipImages'와 같은 순서·개수여야 한다.
    [SerializeField, NonReorderable, Tooltip("(선택) 네모 위의 장비 아이콘 4개. 비우면 색 네모만. 순서는 위와 같다")]
    private Image[] iconImages = new Image[0];

    // 딤·흑백 전의 색 — 칸이 어두워졌다 밝아질 때 돌아갈 값이다('SlotView._socketColors'와 같은 이유).
    private Color[] _baseColors = new Color[0];

    // 네모·아이콘 수 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        if (pipImages.Length != EquipLabel.WornSlots.Length)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"장착 네모가 {pipImages.Length}개다 — 장비 칸은 {EquipLabel.WornSlots.Length}칸이라 자리가 어긋난다.", this);
        }

        if (iconImages.Length > 0 && iconImages.Length != pipImages.Length)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"아이콘이 {iconImages.Length}개 · 네모가 {pipImages.Length}개다 — 개수가 같아야 칸이 맞는다.", this);
        }
    }

    // 칸마다 낀 장비의 등급(+ 아이콘)을 그린다. 'null'이면 줄을 끈다 (칸 View가 매번 그릴 때 호출).
    //   grades : 'WornSlots' 순서의 등급 — 'None'은 빈 칸
    //   icons  : 같은 순서의 아이콘 — 아이콘 자리가 없는 프리팹이면 무시된다. 그림이 없는 칸은 아이콘을 끈다(바탕 색만 남는다)
    public void Bind(IReadOnlyList<GlobalRarity>? grades, IReadOnlyList<Sprite?>? icons = null)
    {
        gameObject.SetActive(grades != null);

        if (grades == null)
        {
            return;
        }

        // 색 자리는 처음 그릴 때 만든다 — 꺼진 채 들어온 칸은 Awake보다 이게 먼저 불린다
        if (_baseColors.Length != pipImages.Length)
        {
            _baseColors = new Color[pipImages.Length];
        }

        for (int i = 0; i < pipImages.Length; i++)
        {
            GlobalRarity grade = i < grades.Count ? grades[i] : GlobalRarity.None;

            _baseColors[i] = grade == GlobalRarity.None ? RarityPalette.EmptySocket : RarityPalette.Get(grade);
        }

        for (int i = 0; i < iconImages.Length; i++)
        {
            Image?  icon   = iconImages[i];
            Sprite? sprite = icons != null && i < icons.Count ? icons[i] : null;

            if (icon == null)
            {
                continue; // 배선 누락은 Awake가 이미 경고했다
            }

            icon.sprite  = sprite;
            icon.enabled = sprite != null;
        }

        SetTint(false, false);
    }

    // 칸의 딤(나가 있음)·흑백(찾기 제외)을 네모에도 입힌다 ('SlotView.ApplyTint'가 호출).
    // ※ 색 규칙은 'SlotView.TintColor' 하나다 — 칸과 네모가 다르게 어두워지면 네모만 떠 보인다.
    public void SetTint(bool dimmed, bool filteredOut)
    {
        // 한 번도 그리지 않았으면 '_baseColors'가 비어 있다 — 꺼진 줄이라 칠할 것도 없다
        for (int i = 0; i < pipImages.Length && i < _baseColors.Length; i++)
        {
            if (pipImages[i] != null)
            {
                pipImages[i].color = SlotView.TintColor(_baseColors[i], dimmed, filteredOut);
            }
        }
    }
}
