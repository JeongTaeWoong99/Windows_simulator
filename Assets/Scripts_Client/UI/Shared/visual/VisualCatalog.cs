using System;
using MikaProtocol;
using UnityEngine;

// 연출 그림 목록 — "이 캐릭터·이 산업은 어떤 그림으로 그리나"를 한 곳에서 답한다.
// 에셋은 'Assets/Art/VisualCatalog.asset' 하나이고, 쓰는 화면이 직접 참조한다.
//
// ■ 빠진 그림은 대체로 버틴다
//   그림이 아직 없는 캐릭터는 'fallbackCharacter'(1번)로, 산업 배경이 없으면 'defaultBackground'로 그린다.
//   빈 슬롯 전용 배경('emptySlotBackground')도 비워 두면 기본 배경이 멈춘 채로 나온다.
//   빠진 목록은 메뉴 'Window/DesktopWindowControl/아트/검사'가 알려 준다.
//
// ■ 캐릭터는 종류(TID)로 찾는다
//   'WorkStationSlotInfo.CharacterId'는 개체 번호라 여기서 못 찾는다 — 보유 목록에서 TID로 바꿔 넘긴다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Visual Catalog", fileName = "VisualCatalog")]
public class VisualCatalog : ScriptableObject
{
    [Serializable]
    public struct CharacterEntry
    {
        [Tooltip("CharacterTable의 TID")]
        public int characterTid;

        public CharacterVisual visual;
    }

    [Serializable]
    public struct IndustryBackground
    {
        public EIndustryType industry;
        public BackgroundVisual visual;
    }

    [Serializable]
    public struct IndustryTarget
    {
        public EIndustryType industry;
        public TargetVisual visual;
    }

    [CenterHeader("캐릭터")]
    [SerializeField, Tooltip("그림이 아직 없는 캐릭터가 대신 쓰는 그림")]
    private CharacterVisual fallbackCharacter = null!;

    [SerializeField]
    private CharacterEntry[] characters = Array.Empty<CharacterEntry>();

    [CenterHeader("배경")]
    [SerializeField, Tooltip("산업 배경이 없을 때 쓰는 배경")]
    private BackgroundVisual defaultBackground = null!;

    [SerializeField, Tooltip("빈 슬롯 전용 배경. 비우면 기본 배경을 멈춘 채로 쓴다")]
    private BackgroundVisual? emptySlotBackground;

    [SerializeField]
    private IndustryBackground[] industryBackgrounds = Array.Empty<IndustryBackground>();

    [CenterHeader("대상")]
    [SerializeField]
    private IndustryTarget[] targets = Array.Empty<IndustryTarget>();

    [CenterHeader("시간 (초)")]
    [SerializeField, Tooltip("달리기 한 바퀴. 프레임 수와 무관하게 이 시간에 맞춰 재생한다")]
    private float runLoopSeconds = 0.8f;

    [SerializeField, Tooltip("공격 한 번. 프레임 수와 무관하게 이 시간에 맞춰 재생한다")]
    private float attackSeconds = 0.6f;

    [SerializeField, Tooltip("대상이 나타나 사거리에 닿기까지")]
    private float approachSeconds = 1f;

    public float RunLoopSeconds  => runLoopSeconds;
    public float AttackSeconds   => attackSeconds;
    public float ApproachSeconds => approachSeconds;

    // 이 종류의 캐릭터 그림. 없으면 1번 그림.
    public CharacterVisual GetCharacter(int characterTid) => FindCharacter(characterTid) ?? fallbackCharacter;

    // 이 종류에 전용 그림이 있는가 (에디터 검사가 대체 그림으로 버티는 캐릭터를 셀 때)
    public bool HasCharacter(int characterTid) => FindCharacter(characterTid) != null;

    // 이 산업의 배경. 없으면 기본 배경.
    public BackgroundVisual GetBackground(EIndustryType industry)
    {
        foreach (IndustryBackground entry in industryBackgrounds)
        {
            if (entry.industry == industry && entry.visual != null)
            {
                return entry.visual;
            }
        }

        return defaultBackground;
    }

    // 빈 슬롯의 배경. 전용 배경이 없으면 기본 배경 — 빈 슬롯은 흐르지 않는다.
    public BackgroundVisual GetEmptySlotBackground()
        => emptySlotBackground != null ? emptySlotBackground : defaultBackground;

    // 이 산업의 대상. 없으면 null — 대상 없이 달리기만 한다.
    public TargetVisual? GetTarget(EIndustryType industry)
    {
        foreach (IndustryTarget entry in targets)
        {
            if (entry.industry == industry && entry.visual != null)
            {
                return entry.visual;
            }
        }

        return null;
    }

    private CharacterVisual? FindCharacter(int characterTid)
    {
        foreach (CharacterEntry entry in characters)
        {
            if (entry.characterTid == characterTid && entry.visual != null)
            {
                return entry.visual;
            }
        }

        return null;
    }
}
