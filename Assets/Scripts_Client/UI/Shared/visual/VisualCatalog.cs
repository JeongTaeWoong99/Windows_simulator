using System;
using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;

// 그림 목록 — "이 캐릭터·이 산업·이 아이템은 어떤 그림으로 그리나"를 한 곳에서 답한다.
// 에셋은 'Assets/Art/Resources/VisualCatalog.asset' 하나다. 화면은 'Current'(Resources)로 찾는다 —
// 그림 저장소를 받지 않은 PC에는 없으므로 null이고, 그때 화면은 지금까지의 자리 표시(색 네모·글자)를 그린다.
//
// ■ 누가 채우나
//   손으로 채우지 않는다 — 메뉴 '아트/전부 다시 굽기'가 끝에 폴더를 훑어 다시 쓴다('ArtCatalogSync').
//   캐릭터 = 레시피의 'characterTids'(CharacterTable TID, 0이면 대체 그림) · 아이콘 = 파일 이름의 TID.
//   이펙트 = 'fx/<연출 키>/<키>_<부위>.png' — 파일 이름이 곧 찾는 이름('FxOf("reveal_glow")'). 메뉴 '아트/목록만 다시 쓰기'로도 채운다.
//
// ■ 이펙트는 없으면 null — 그 효과만 빠진다
//   쓰는 쪽('FxSprites' 이름표를 거쳐)은 null이면 그 Image를 켜지 않는다. 그림 저장소가 없어도 연출의 움직임은 돈다.
//
// ■ 빠진 그림은 대체로 버틴다
//   그림이 아직 없는 캐릭터는 'fallbackCharacter'(TID 0으로 등록한 그림)로, 산업 배경이 없으면 'defaultBackground'로 그린다.
//   아이콘이 없는 자원·장비는 0번 아이콘('item_0' · 'equip_0')으로 그린다.
//   빈 슬롯 전용 배경('emptySlotBackground')도 비워 두면 기본 배경이 멈춘 채로 나온다.
//   빠진 목록은 메뉴 'Window/DesktopWindowControl/아트/검사'가 알려 준다.
//
// ■ 시간·거리 값은 여기 없다
//   그림과 함께 git 밖에 있으면 안 돼서 'SlotStageSettings'(Resources)로 뺐다.
//
// ■ 캐릭터는 종류(TID)로 찾는다
//   'WorkStationSlotInfo.CharacterId'는 개체 번호라 여기서 못 찾는다 — 보유 목록에서 TID로 바꿔 넘긴다.
[CreateAssetMenu(menuName = "DesktopWindowControl/Visual/Visual Catalog", fileName = "VisualCatalog")]
public class VisualCatalog : ScriptableObject
{
    // 'Resources.Load' 경로 — 'Assets/Art/Resources/VisualCatalog.asset'
    public const string ResourcePath = "VisualCatalog";

    [Serializable]
    public struct IconEntry
    {
        [Tooltip("ItemTable · EquipTable의 TID")]
        public int tid;

        public Sprite icon;
    }

    [Serializable]
    public struct FxEntry
    {
        [Tooltip("파일 이름 — 'fx/<연출 키>/<키>_<부위>.png'의 '<키>_<부위>'")]
        public string key;

        public Sprite sprite;
    }

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

    [CenterHeader("아이콘")]
    [SerializeField, Tooltip("아이콘이 아직 없는 자원이 대신 쓰는 아이콘 ('icons/items/item_0')")]
    private Sprite? fallbackItemIcon;

    [SerializeField, Tooltip("자원 아이콘 ('icons/items/item_<TID>')")]
    private IconEntry[] itemIcons = Array.Empty<IconEntry>();

    [SerializeField, Tooltip("아이콘이 아직 없는 장비가 대신 쓰는 아이콘 ('icons/equips/equip_0')")]
    private Sprite? fallbackEquipIcon;

    [SerializeField, Tooltip("장비 아이콘 ('icons/equips/equip_<TID>')")]
    private IconEntry[] equipIcons = Array.Empty<IconEntry>();

    [CenterHeader("이펙트")]
    [SerializeField, Tooltip("이펙트 그림 ('fx/<연출 키>/<키>_<부위>') — 흰색으로 그리고 색은 코드가 입힌다")]
    private FxEntry[] fxSprites = Array.Empty<FxEntry>();

    // 아이콘은 칸마다 찾으므로 표로 바꿔 둔다 (처음 찾을 때 만든다)
    private Dictionary<int, Sprite>? _itemIconMap;
    private Dictionary<int, Sprite>? _equipIconMap;
    private Dictionary<string, Sprite>? _fxMap;

    #region 화면이 부르는 곳 — 목록이 없으면(그림 저장소 없음) null

    private static VisualCatalog? _current;
    private static bool           _searched;

    // 지금 쓰는 목록. 그림 저장소를 받지 않은 PC면 null
    public static VisualCatalog? Current
    {
        get
        {
            if (!_searched)
            {
                _searched = true;
                _current  = Resources.Load<VisualCatalog>(ResourcePath);
            }

            return _current;
        }
    }

    // 자원 아이콘 — 없으면 0번 아이콘, 목록이 없으면 null
    public static Sprite? ItemIconOf(int itemTid) => Current != null ? Current.GetItemIcon(itemTid) : null;

    // 장비 아이콘 — 없으면 0번 아이콘, 목록이 없으면 null
    public static Sprite? EquipIconOf(int equipTid) => Current != null ? Current.GetEquipIcon(equipTid) : null;

    // 캐릭터 상반신 (인벤토리) — 그림 없는 캐릭터는 대체 캐릭터의 것
    public static Sprite? PortraitOf(int characterTid) => Current != null ? Current.GetCharacter(characterTid).Portrait : null;

    // 캐릭터 머리 (위젯) — 그림 없는 캐릭터는 대체 캐릭터의 것
    public static Sprite? HeadOf(int characterTid) => Current != null ? Current.GetCharacter(characterTid).Head : null;

    // 이펙트 그림 — 파일 이름('reveal_glow')으로 찾는다. 없거나 목록이 없으면 null
    public static Sprite? FxOf(string key) => Current != null ? Current.GetFx(key) : null;

    #endregion

    public Sprite? GetFx(string key) => (_fxMap ??= ToFxMap(fxSprites)).TryGetValue(key, out Sprite sprite) ? sprite : null;

    public Sprite? GetItemIcon(int itemTid)
        => (_itemIconMap ??= ToMap(itemIcons)).TryGetValue(itemTid, out Sprite icon) ? icon : fallbackItemIcon;

    public Sprite? GetEquipIcon(int equipTid)
        => (_equipIconMap ??= ToMap(equipIcons)).TryGetValue(equipTid, out Sprite icon) ? icon : fallbackEquipIcon;

    // 이 TID에 전용 아이콘이 있는가 (에디터 검사가 0번으로 버티는 것을 셀 때)
    public bool HasItemIcon(int itemTid)   => (_itemIconMap  ??= ToMap(itemIcons)).ContainsKey(itemTid);
    public bool HasEquipIcon(int equipTid) => (_equipIconMap ??= ToMap(equipIcons)).ContainsKey(equipTid);

    // 플레이 중에 목록을 다시 써도(굽기) 표가 낡지 않게 (Unity 메시지)
    private void OnValidate()
    {
        _itemIconMap  = null;
        _equipIconMap = null;
        _fxMap        = null;
    }

    private static Dictionary<string, Sprite> ToFxMap(FxEntry[] entries)
    {
        var map = new Dictionary<string, Sprite>(entries.Length);

        foreach (FxEntry entry in entries)
        {
            if (entry.sprite != null && !string.IsNullOrEmpty(entry.key))
            {
                map[entry.key] = entry.sprite;
            }
        }

        return map;
    }

    private static Dictionary<int, Sprite> ToMap(IconEntry[] entries)
    {
        var map = new Dictionary<int, Sprite>(entries.Length);

        foreach (IconEntry entry in entries)
        {
            if (entry.icon != null)
            {
                map[entry.tid] = entry.icon;
            }
        }

        return map;
    }

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
