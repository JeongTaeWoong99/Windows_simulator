using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CharacterInfo = MikaProtocol.CharacterInfo;

// 우편함 — 서버가 맡겨 둔 보상(운영 지급 · 인벤토리 넘침 보관)을 보고 받고 지운다.
//
// ■ 탭 두 개 — [안 받은 우편] · [받은 우편] (2026-10-01)
// 한 목록에 섞어 두면 받은 우편이 쌓일수록 받을 것이 묻힌다. 탭마다 할 일이 다르다 —
// 안 받은 우편은 [받기]·[모두 받기], 받은 우편은 [삭제]뿐이고 7일 뒤 서버가 지운다(기획 우편 1장 9번).
// 탭 이름에 개수를 붙여, 다른 탭에 무엇이 있는지 열지 않고도 보인다.
//
// ■ 목록 순서
// 안 받은 우편은 새로 온 것이 위, 받은 우편은 최근에 받은 것이 위다.
// 모두 받기는 서버가 **오래된 것부터** 받는다 — 화면 순서와 다르지만, 보여 주는 순서와 받는 순서는 별개다.
//
// ■ 줄마다 첨부 아이콘(첫 첨부 · 나머지 종류 수)과 툴팁(첨부 전부)이 붙는다 — 줄의 요약 문구는 길면 잘린다.
//
// ■ 요청은 한 번에 하나
// 받기·삭제·모두 받기가 대기 하나를 나눠 쓴다. 응답이 오기 전에는 버튼이 전부 잠긴다
// — 같은 우편에 받기를 두 번 보내면 두 번째가 'MailAlreadyClaimed'로 돌아와 알림만 하나 더 뜬다.
//
// ■ "인벤토리 부족"은 여기서 따로 쓴다
// 결과 코드는 'StorageFull' 하나지만, 뽑기·상자와 달리 우편은 **아무것도 지급되지 않았고
// 우편도 그대로 남는다.** 공용 문구('ResultMessages')로는 그 차이가 안 전해진다.
//
// 기획은 'GameDesign/design/mail/README.md', 화면 흐름은 'Main 규칙.md'.
public class MailPresenter : MonoBehaviour
{
    [CenterHeader("Header Panel")]
    [SerializeField, Tooltip("'안 받은 우편 n통' 문구")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("작업슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    [CenterHeader("Tab Panel")]
    [SerializeField, Tooltip("[안 받은 우편] 탭 버튼. OnClick은 코드가 연결한다")]
    private Button unclaimedTabButton = null!;

    [SerializeField, Tooltip("[안 받은 우편] 탭 문구 — 개수가 붙는다")]
    private TMP_Text unclaimedTabText = null!;

    [SerializeField, Tooltip("[받은 우편] 탭 버튼. OnClick은 코드가 연결한다")]
    private Button claimedTabButton = null!;

    [SerializeField, Tooltip("[받은 우편] 탭 문구 — 개수가 붙는다")]
    private TMP_Text claimedTabText = null!;

    [SerializeField, Tooltip("지금 열린 탭 버튼 색")]
    private UIThemeRole selectedTabRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열리지 않은 탭 버튼 색")]
    private UIThemeRole normalTabRole = UIThemeRole.Button;

    [CenterHeader("Body Scroll Panel")]
    [SerializeField, Tooltip("우편 한 줄 프리팹")]
    private MailRowView rowPrefab = null!;

    [SerializeField, Tooltip("줄이 쌓이는 Content (VLG + ContentSizeFitter)")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("지금 탭에 우편이 한 통도 없을 때만 보인다. 문구는 탭마다 코드가 바꾼다")]
    private TMP_Text emptyText = null!;

    [CenterHeader("Footer Panel")]
    [SerializeField, Tooltip("안 받은 우편을 오래된 것부터 전부 받는다. OnClick은 코드가 연결한다")]
    private Button claimAllButton = null!;

    [SerializeField, Tooltip("아래 안내 한 줄 — 탭마다 보관 규칙을 적는다(안 받은 우편은 사라지지 않음 · 받은 우편은 7일 뒤 삭제)")]
    private TMP_Text footerNoteText = null!;

    // 우편함의 탭 — 받았는지로 가른다.
    private enum MailTab
    {
        Unclaimed, // 안 받은 우편 — [받기] · [모두 받기]
        Claimed,   // 받은 우편 — [삭제] · 7일 뒤 서버가 지운다
    }

    // 받은 우편이 남는 기간 — 기획 우편 1장 9번. 지우는 것은 서버다 — 여기선 "며칠 남았나"를 적는 데만 쓴다.
    private const int ClaimedKeepDays = 7;

    private readonly List<MailRowView> _rows   = new List<MailRowView>();
    private readonly List<MailInfo>    _sorted = new List<MailInfo>();

    // 지금 열린 탭 — 화면 상태라 저장하지 않는다. 다시 열면 안 받은 우편부터다.
    private MailTab _tab = MailTab.Unclaimed;

    private PlayerDataModel   _data    = null!;
    private UIManager         _ui      = null!;
    private NetworkManager    _network = null!;
    private ServerWaitManager _wait    = null!;

    private ServerWaitHandle? _waitHandle;
    private bool              _isClaimAll; // 지금 기다리는 요청이 모두 받기인가 — 결과 문구가 갈린다
    private bool              _isSubscribed;
    private bool              _isReady;    // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (Unity 메시지)
    private void Start()
    {
        this.RequireRef(countText,      nameof(countText));
        this.RequireRef(backButton,     nameof(backButton));
        this.RequireRef(rowPrefab,      nameof(rowPrefab));
        this.RequireRef(rowParent,      nameof(rowParent));
        this.RequireRef(emptyText,      nameof(emptyText));
        this.RequireRef(claimAllButton, nameof(claimAllButton));
        this.RequireRef(footerNoteText, nameof(footerNoteText));
        this.RequireRef(unclaimedTabButton, nameof(unclaimedTabButton));
        this.RequireRef(unclaimedTabText,   nameof(unclaimedTabText));
        this.RequireRef(claimedTabButton,   nameof(claimedTabButton));
        this.RequireRef(claimedTabText,     nameof(claimedTabText));

        _data    = Services.Get<PlayerDataModel>();
        _ui      = Services.Get<UIManager>();
        _network = NetworkManager.Instance;
        _wait    = Services.Get<ServerWaitManager>();

        // 이 화면을 직접 끄지 않는다 — 'SettingPresenter'의 뒤로가기와 같은 이유('Main 규칙.md'의 "전환 층은 하나다").
        backButton.onClick.AddListener(() => _ui.ShowMainScreen(MainScreen.WorkStationList));
        claimAllButton.onClick.AddListener(OnClaimAllClicked);
        unclaimedTabButton.onClick.AddListener(() => ShowTab(MailTab.Unclaimed));
        claimedTabButton.onClick.AddListener(() => ShowTab(MailTab.Claimed));

        Subscribe();
        Refresh(); // 이미 우편함을 받은 뒤에 처음 열렸을 수 있다

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 — 닫혀 있는 동안 온 우편을 놓쳤으니 다시 그린다 (Unity 메시지)
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Refresh();
    }

    // 구독 해제 (Unity 메시지)
    // ※ 대기 중에 화면을 닫아도 대기는 그대로 둔다 — 응답을 못 받으면 'ServerWaitManager'가 타임아웃으로 닫는다.
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 우편함 변경·수령·삭제 결과 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed              = true;
        _data.MailsChanged        += Refresh;
        _data.MailClaimCompleted  += OnMailClaimCompleted;
        _data.MailDeleteCompleted += OnMailDeleteCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed              = false;
        _data.MailsChanged        -= Refresh;
        _data.MailClaimCompleted  -= OnMailClaimCompleted;
        _data.MailDeleteCompleted -= OnMailDeleteCompleted;
    }

    #endregion

    #region 표시

    // 탭을 바꿔 다시 그린다 (탭 버튼 OnClick).
    private void ShowTab(MailTab tab)
    {
        _tab = tab;
        Refresh();
    }

    // 목록·개수·탭·모두 받기 버튼을 현재 우편함으로 다시 그린다 (Start · OnEnable · MailsChanged 구독 · 탭 전환)
    private void Refresh()
    {
        bool showClaimed    = _tab == MailTab.Claimed;
        int  unclaimedCount = 0;
        int  claimedCount   = 0;

        _sorted.Clear();

        foreach (MailInfo mail in _data.Mails)
        {
            bool isClaimed = mail.ClaimedAtUnixMs != 0L;

            if (isClaimed)
            {
                claimedCount++;
            }
            else
            {
                unclaimedCount++;
            }

            if (isClaimed == showClaimed)
            {
                _sorted.Add(mail);
            }
        }

        _sorted.Sort(showClaimed ? CompareByClaimedDesc : CompareByReceivedDesc);

        for (int i = 0; i < _sorted.Count; i++)
        {
            MailInfo mail = _sorted[i];
            MailRowView row = GetOrCreateRow(i);

            row.gameObject.SetActive(true);
            row.Bind(mail.MailId, BuildTitle(mail), BuildInfo(mail, showClaimed), BuildAttachment(mail), showClaimed,
                BuildIcon(mail), () => BuildTooltip(mail));
        }

        HideRowsFrom(_sorted.Count);

        countText.text        = $"안 받은 우편 {unclaimedCount}통";
        unclaimedTabText.text = $"안 받은 우편 ({unclaimedCount})";
        claimedTabText.text   = $"받은 우편 ({claimedCount})";

        emptyText.gameObject.SetActive(_sorted.Count == 0);
        emptyText.text = showClaimed ? "받은 우편이 없습니다." : "받을 우편이 없습니다.";

        // 모두 받기는 안 받은 탭에서만 뜻이 있다 — 받은 탭에서는 자리를 안내 문구에 내준다.
        claimAllButton.gameObject.SetActive(!showClaimed);
        footerNoteText.text = showClaimed
            ? $"받은 우편은 받은 뒤 {ClaimedKeepDays}일이 지나면 자동으로 지워집니다."
            : "안 받은 우편은 기한 없이 남습니다.";

        ApplyTabColors();
        ApplyInteractable();

        // 방금 만든 줄은 아직 프리팹 크기 그대로다 — 'SellCartPresenter.Refresh'와 같은 이유로 미리 태운다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 새로 온 것이 위 (안 받은 탭).
    private static int CompareByReceivedDesc(MailInfo a, MailInfo b)
        => b.ReceivedAtUnixMs.CompareTo(a.ReceivedAtUnixMs);

    // 최근에 받은 것이 위 (받은 탭) — 방금 받은 것을 바로 확인하는 자리다.
    private static int CompareByClaimedDesc(MailInfo a, MailInfo b)
        => b.ClaimedAtUnixMs.CompareTo(a.ClaimedAtUnixMs);

    // 탭 버튼의 선택 표시 — 'AuctionTabPresenter'와 같이 ColorBlock을 바꾼다(Image.color는 틴트가 곱해져 탁해진다).
    private void ApplyTabColors()
    {
        SetTabColor(unclaimedTabButton, _tab == MailTab.Unclaimed);
        SetTabColor(claimedTabButton,   _tab == MailTab.Claimed);
    }

    private void SetTabColor(Button button, bool on)
    {
        Color target = UIThemePalette.Of(on ? selectedTabRole : normalTabRole);

        ColorBlock colors = button.colors;
        colors.normalColor   = target;
        colors.selectedColor = target;
        button.colors        = colors;
    }

    // 버튼 잠금을 지금 상태에 맞춘다 — 기다리는 요청이 있으면 전부 잠그고, 받을 우편이 없으면 모두 받기를 잠근다.
    private void ApplyInteractable()
    {
        bool isIdle = _waitHandle == null;

        claimAllButton.interactable = isIdle && _data.HasUnclaimedMail;

        foreach (MailRowView row in _rows)
        {
            row.SetInteractable(isIdle);
        }
    }

    // 제목 — 템플릿에서 읽는다. 표에 없는 템플릿이면 번호라도 보인다.
    private static string BuildTitle(MailInfo mail)
    {
        return GameDataLoader.TryGetMailTemplate(mail.TemplateTid, out MailTemplateTableRow row)
            ? row.Title
            : $"우편 #{mail.TemplateTid}";
    }

    // "발신자 · 도착 시각" — 받은 우편이면 "받음 · n일 뒤 삭제"를 붙인다.
    // ※ 안 받은 우편에는 기한을 적지 않는다 — 만료되지 않는다(기획 우편 1장 8번).
    private static string BuildInfo(MailInfo mail, bool isClaimed)
    {
        string sender   = GetSender(mail);
        string received = FormatTime(mail.ReceivedAtUnixMs);
        string info     = string.IsNullOrEmpty(sender)
            ? received
            : $"{UIRichText.Label(sender)}{UIRichText.Dot}{received}";

        if (!isClaimed)
        {
            return info;
        }

        return $"{info}{UIRichText.Dot}{UIRichText.Paint(FormatDeleteIn(mail.ClaimedAtUnixMs), UIThemeRole.TextDisabled)}";
    }

    // 받은 우편이 지워질 때까지 — "6일 뒤 삭제" · 하루 안이면 "오늘 삭제".
    private static string FormatDeleteIn(long claimedAtUnixMs)
    {
        DateTimeOffset deleteAt = DateTimeOffset.FromUnixTimeMilliseconds(claimedAtUnixMs).AddDays(ClaimedKeepDays);
        int            days     = (int)Math.Ceiling((deleteAt - DateTimeOffset.UtcNow).TotalDays);

        return days <= 1 ? "오늘 삭제" : $"{days}일 뒤 삭제";
    }

    private static string GetSender(MailInfo mail)
        => GameDataLoader.TryGetMailTemplate(mail.TemplateTid, out MailTemplateTableRow row) ? row.Sender : "";

    private static string FormatTime(long unixMs)
        => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    // 줄 왼쪽 아이콘 — 첫 첨부 하나를 그리고, 첨부 종류가 더 있으면 모서리에 '+n'을 적는다.
    // 순서는 첨부 요약과 같다 — 골드 → 자원 → 캐릭터 → 장비.
    private static ItemIconContent BuildIcon(MailInfo mail)
    {
        var  icons = new List<ItemIconContent>();

        if (mail.Gold > 0L)
        {
            icons.Add(ItemIconContent.ForGold());
        }

        if (mail.Items != null)
        {
            foreach (ItemInfo item in mail.Items)
            {
                icons.Add(ItemIconContent.ForItem(item.ItemId, item.Count));
            }
        }

        if (mail.CharacterTids != null)
        {
            foreach (int tid in mail.CharacterTids)
            {
                icons.Add(ItemIconContent.ForCharacter(tid));
            }
        }

        if (mail.Characters != null)
        {
            foreach (CharacterInfo character in mail.Characters)
            {
                icons.Add(ItemIconContent.ForCharacter(character.CharacterTid));
            }
        }

        if (mail.EquipTids != null)
        {
            foreach (int tid in mail.EquipTids)
            {
                icons.Add(ItemIconContent.ForEquip(tid, null));
            }
        }

        if (mail.Equips != null)
        {
            foreach (EquipInfo equip in mail.Equips)
            {
                icons.Add(ItemIconContent.ForEquip(equip.EquipTid, equip.EnchantOptions));
            }
        }

        if (icons.Count == 0)
        {
            return new ItemIconContent(GlobalRarity.None, "-", "", null);
        }

        ItemIconContent first = icons[0];

        return icons.Count == 1
            ? first
            : new ItemIconContent(first.Rarity, first.Glyph, $"+{icons.Count - 1}", first.Sockets);
    }

    // 우편 툴팁 — 발신 · 도착 · (받은 시각 · 삭제까지) · 첨부 전부(이름은 등급색, 장비는 능력치 칸 수).
    // 줄의 첨부 요약은 길면 잘린다 — 여기서 빠짐없이 적는다.
    private static TooltipContent BuildTooltip(MailInfo mail)
    {
        var content = new TooltipContent(BuildTitle(mail));
        string sender = GetSender(mail);

        if (!string.IsNullOrEmpty(sender))
        {
            content.Row("발신", sender);
        }

        content.Row("도착", FormatTime(mail.ReceivedAtUnixMs));

        if (mail.ClaimedAtUnixMs != 0L)
        {
            content.Row("받음", FormatTime(mail.ClaimedAtUnixMs), FormatDeleteIn(mail.ClaimedAtUnixMs), null);
        }

        content.Header("첨부");

        int before = content.Lines.Count;

        if (mail.Gold > 0L)
        {
            content.Row("골드", $"{mail.Gold:N0}");
        }

        if (mail.Items != null)
        {
            foreach (ItemInfo item in mail.Items)
            {
                GlobalRarity rarity = GameDataLoader.GetItemRarity(item.ItemId);

                content.Row(UIRichText.Paint(GameDataLoader.GetItemName(item.ItemId), RarityPalette.Get(rarity)), $"x{item.Count:N0}", RarityLabel.Get(rarity), null);
            }
        }

        if (mail.CharacterTids != null)
        {
            foreach (int tid in mail.CharacterTids)
            {
                GlobalRarity rarity = GameDataLoader.GetCharacterRarity(tid);

                content.Row(UIRichText.Paint(GameDataLoader.GetCharacterName(tid), RarityPalette.Get(rarity)), "캐릭터", RarityLabel.Get(rarity), null);
            }
        }

        // 개체 캐릭터(경매 구매·반환) — 레벨·대표 적성까지 적는다. 같은 종류라도 개체마다 값이 다르다.
        if (mail.Characters != null)
        {
            foreach (CharacterInfo character in mail.Characters)
            {
                GlobalRarity rarity = GameDataLoader.GetCharacterRarity(character.CharacterTid);

                content.Row(UIRichText.Paint(GameDataLoader.GetCharacterName(character.CharacterTid), RarityPalette.Get(rarity)), AuctionText.FormatCharacterSummary(character), RarityLabel.Get(rarity), null);
            }
        }

        if (mail.EquipTids != null)
        {
            foreach (int tid in mail.EquipTids)
            {
                AddEquipLine(content, tid, null);
            }
        }

        if (mail.Equips != null)
        {
            foreach (EquipInfo equip in mail.Equips)
            {
                AddEquipLine(content, equip.EquipTid, equip.EnchantOptions);
            }
        }

        if (content.Lines.Count == before)
        {
            content.Row("첨부 없음", "");
        }

        return content;
    }

    // 장비 첨부 한 줄 — 이름(등급색) · 기본 능력치 · 능력치 칸 수 (BuildTooltip에서 호출).
    private static void AddEquipLine(TooltipContent content, int equipTid, IReadOnlyList<int>? optionTids)
    {
        GlobalRarity rarity = GameDataLoader.GetEquipRarity(equipTid);

        content.Row(
            UIRichText.Paint(GameDataLoader.GetEquipName(equipTid), RarityPalette.Get(rarity)),
            EquipLabel.GetEffectText(equipTid),
            AuctionText.FormatStatSummary(equipTid, optionTids),
            null);
    }

    // 첨부 요약 — "골드 1,000 · 나무 x10 · 무사 x2 · 낡은 곡괭이".
    // ※ 템플릿이 아니라 패킷의 첨부를 쓴다 — 넘침 보관 우편은 템플릿 첨부가 비어 있다('GameDataLoader.TryGetMailTemplate').
    private static string BuildAttachment(MailInfo mail)
    {
        var parts = new List<string>();

        if (mail.Gold > 0L)
        {
            parts.Add($"골드 {mail.Gold:N0}");
        }

        if (mail.Items != null)
        {
            foreach (ItemInfo item in mail.Items)
            {
                parts.Add($"{GameDataLoader.GetItemName(item.ItemId)} x{item.Count:N0}");
            }
        }

        AddGrouped(parts, mail.CharacterTids, tid => GameDataLoader.GetCharacterName(tid));

        if (mail.Characters != null)
        {
            foreach (CharacterInfo character in mail.Characters)
            {
                parts.Add(GameDataLoader.GetCharacterName(character.CharacterTid));
            }
        }
        AddGrouped(parts, mail.EquipTids,     GameDataLoader.GetEquipName);

        if (mail.Equips != null)
        {
            foreach (EquipInfo equip in mail.Equips)
            {
                parts.Add(GameDataLoader.GetEquipName(equip.EquipTid));
            }
        }

        return parts.Count == 0 ? "첨부 없음" : string.Join(" · ", parts);
    }

    // 한 개체 = 한 원소로 오는 TID 목록을 종류별로 묶어 "이름 x수"로 붙인다 (처음 나온 순서를 지킨다).
    private static void AddGrouped(List<string> parts, List<int>? tids, Func<int, string> nameOf)
    {
        if (tids == null || tids.Count == 0)
        {
            return;
        }

        var order  = new List<int>();
        var counts = new Dictionary<int, int>();

        foreach (int tid in tids)
        {
            if (counts.TryGetValue(tid, out int count))
            {
                counts[tid] = count + 1;
            }
            else
            {
                counts.Add(tid, 1);
                order.Add(tid);
            }
        }

        foreach (int tid in order)
        {
            parts.Add(counts[tid] > 1 ? $"{nameOf(tid)} x{counts[tid]}" : nameOf(tid));
        }
    }

    #endregion

    #region 요청

    // 한 통 받기 (MailRowView.ClaimClicked 구독)
    private void OnRowClaimClicked(long mailId)
    {
        SendClaim(mailId, "우편 받기");
    }

    // 모두 받기 (claimAllButton.onClick) — 'MailId = 0'이 모두 받기다.
    private void OnClaimAllClicked()
    {
        SendClaim(0L, "모두 받기");
    }

    // 받은 우편 지우기 (MailRowView.DeleteClicked 구독)
    // ※ 확인 창을 띄우지 않는다 — 지울 수 있는 건 이미 받은 우편뿐이라 잃는 것이 없고, 7일 뒤면 어차피 사라진다.
    private void OnRowDeleteClicked(long mailId)
    {
        if (!CanSend())
        {
            return;
        }

        _network.Send(new C_MailDeleteRequest { MailId = mailId });
        ClientLogger.Info(ClientLogger.Send, $"우편 삭제 요청 — 우편={mailId}");

        BeginWait("우편 삭제", isClaimAll: false);
    }

    // 수령 요청을 보낸다 (한 통 · 모두 받기 공통).
    private void SendClaim(long mailId, string label)
    {
        if (!CanSend())
        {
            return;
        }

        _network.Send(new C_MailClaimRequest { MailId = mailId });
        ClientLogger.Info(ClientLogger.Send, mailId == 0L ? "우편 모두 받기 요청" : $"우편 수령 요청 — 우편={mailId}");

        BeginWait(label, isClaimAll: mailId == 0L);
    }

    // 지금 보내도 되는가 — 기다리는 요청이 있거나 로그인 전이면 보내지 않는다.
    // 로그인 전 요청은 서버가 응답 없이 버려 "눌렀는데 아무 일도 없다"로만 보인다 — 여기서 끊고 이유를 남긴다.
    private bool CanSend()
    {
        if (_waitHandle != null)
        {
            return false;
        }

        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "우편 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return false;
        }

        return true;
    }

    // 대기를 열고 버튼을 잠근다 (요청을 보낸 직후).
    private void BeginWait(string label, bool isClaimAll)
    {
        _isClaimAll = isClaimAll;
        _waitHandle = _wait.Begin(label, onClosed: OnWaitClosed);

        ApplyInteractable();
    }

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 버튼 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _waitHandle = null;
        _isClaimAll = false;

        ApplyInteractable();
    }

    #endregion

    #region 응답

    // 수령 결과 도착 (PlayerDataModel.MailClaimCompleted 구독)
    // ※ 목록은 'MailsChanged'로, 받은 것은 결과 팝업('GachaResultPresenter')이 'MailRewardsClaimed'로 이미 보여 줬다
    //   — 여기서는 대기를 닫고 실패 문구만 정한다. 성공 알림을 따로 띄우면 결과 팝업 위에 창이 하나 더 겹친다.
    private void OnMailClaimCompleted(EResultCode code, int claimedCount, int remainingCount)
    {
        if (_waitHandle == null)
        {
            return; // 내가 보낸 요청이 아니다(타임아웃 뒤 늦게 온 응답 등)
        }

        bool isClaimAll = _isClaimAll;

        if (code == EResultCode.Ok)
        {
            _waitHandle.Succeed();

            return;
        }

        if (code == EResultCode.StorageFull)
        {
            string message = isClaimAll && claimedCount > 0
                ? $"인벤토리가 부족해 우편 {claimedCount}통만 받았습니다.\n남은 {remainingCount}통은 인벤토리를 정리한 뒤 받아 주세요."
                : "인벤토리가 부족합니다.\n인벤토리를 정리한 뒤 다시 받아 주세요.";

            _waitHandle.Fail(message);

            return;
        }

        _waitHandle.Fail(ResultMessages.ToText(code));
    }

    // 삭제 결과 도착 (PlayerDataModel.MailDeleteCompleted 구독)
    private void OnMailDeleteCompleted(bool success, EResultCode code)
    {
        if (_waitHandle == null)
        {
            return;
        }

        if (success)
        {
            _waitHandle.Succeed();
        }
        else
        {
            _waitHandle.Fail(ResultMessages.ToText(code));
        }
    }

    #endregion

    #region 줄 풀

    // 'index'번째 줄을 돌려준다. 아직 없으면 그때 만든다 (Refresh에서 호출).
    private MailRowView GetOrCreateRow(int index)
    {
        if (index < _rows.Count)
        {
            return _rows[index];
        }

        MailRowView row = Instantiate(rowPrefab, rowParent);

        // 줄은 파괴하지 않고 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        row.ClaimClicked  += OnRowClaimClicked;
        row.DeleteClicked += OnRowDeleteClicked;

        _rows.Add(row);

        return row;
    }

    // 이번에 쓰이지 않은 줄을 비우고 꺼 둔다 (Refresh에서 호출).
    private void HideRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _rows.Count; i++)
        {
            _rows[i].Clear();
            _rows[i].gameObject.SetActive(false);
        }
    }

    #endregion
}
