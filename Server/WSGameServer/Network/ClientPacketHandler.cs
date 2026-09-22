using MikaNetwork;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 클라이언트 요청 핸들러. 전부 <b>로직 스레드</b>에서 실행된다.
///
/// <para>
/// "무슨 패킷이 언제 어느 스레드로 들어왔는가"는 패킷 로그(<see cref="PacketLogger"/>)가 이미 남긴다.
/// 여기서는 <b>요청의 내용</b>만 덧붙인다 — 같은 정보를 두 줄로 찍지 않는다.
/// </para>
/// </summary>
public static class ClientPacketHandler
{
    [PacketHandler]
    public static void Handle_C_EchoRequest(ISession session, C_EchoRequest req)
    {
        ServerLog.Debug("에코", $"sid={session.SessionId} \"{req.Message}\"");

        // 응답도 객체로 송신 (직렬화/프레이밍은 SendPacket이 처리)
        session.SendPacket(new S_EchoResponse { Message = req.Message });
    }

    [PacketHandler]
    public static void Handle_C_PingRequest(ISession session, C_PingRequest req)
    {
        session.SendPacket(new S_PongResponse());
    }


    [PacketHandler]
    public static void Handle_C_LoginRequest(ISession session, C_LoginRequest req)
    {
        // DB 스레드에서 조회/자동가입 → 로직 스레드에서 User 등록·응답 (LoginRepository.Apply)
        ServerLog.Info("로그인", $"요청 Id={req.Id} sessionId={session.SessionId}");

        UserManager.Instance.CreateUser(session, req.Id, req.Id);
    }

    [PacketHandler]
    public static void Handle_C_AddItemRequest(ISession session, C_AddItemRequest req)
    {
        ServerLog.Debug("아이템", $"지급 요청 ItemId={req.ItemId} 개수={req.Count} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_UpdateItemResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        user.AddItem(req.ItemId, req.Count);
    }

    [PacketHandler]
    public static void Handle_C_GachaDrawRequest(ISession session, C_GachaDrawRequest req)
    {
        ServerLog.Debug("가챠", $"뽑기 요청 GachaId={req.GachaId} 횟수={req.DrawCount} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_GachaDrawResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        GachaService.Instance.Draw(user, req.GachaId, req.DrawCount);
    }

    /// <summary>치트 — 권한 검사와 실행은 User가 한다. 여기서는 로그인 여부만 본다.</summary>
    [PacketHandler]
    public static void Handle_C_CheatRequest(ISession session, C_CheatRequest req)
    {
        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_CheatResponse { Result = EResultCode.NotLoggedIn, Command = req.Command });
            return;
        }

        user.ExecuteCheat(req, DateTime.UtcNow);
    }

    /// <summary>
    /// 작업슬롯에 산업·캐릭터를 배치한다.
    /// <b>클라이언트가 요청하는 것은 "배치"뿐이고, 무엇이 몇 개 나오는지는 서버가 정한다.</b>
    /// </summary>
    [PacketHandler]
    public static void Handle_C_WorkStationAssignRequest(ISession session, C_WorkStationAssignRequest req)
    {
        ServerLog.Debug("작업슬롯",
            $"배치 요청 슬롯={req.SlotIndex} 산업={(GameData.IndustryType)req.Industry} Lv{req.IndustryLevel} " +
            $"캐릭터={req.CharacterId} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_WorkStationAssignResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        // 레벨 필드를 채우지 않는 구 클라이언트는 0을 보낸다. 0은 "미지정"으로 보고 기본 레벨로 돌린다 —
        // 우회가 아니다(기본 레벨은 항상 열려 있다). 그 위의 값은 User가 해금 여부를 검증한다.
        var industryLevel = req.IndustryLevel == 0
            ? WorkStationSlot.DefaultIndustryLevel
            : req.IndustryLevel;

        user.AssignWorkStation(req.SlotIndex, (GameData.IndustryType)req.Industry, req.CharacterId,
                               DateTime.UtcNow, industryLevel);
    }

    /// <summary>적성 포인트 찍기. 포인트·상한 검증은 User가 한다 — 클라가 "찍을 수 있다"고 그렸어도 여기서 다시 검사한다.</summary>
    [PacketHandler]
    public static void Handle_C_AptitudeUpRequest(ISession session, C_AptitudeUpRequest req)
    {
        ServerLog.Debug("캐릭터", $"적성 찍기 요청 Character={req.CharacterId} 산업={req.Industry} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_AptitudeUpResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        user.RaiseAptitude(req.CharacterId, (GameData.IndustryType)req.Industry, DateTime.UtcNow);
    }

    /// <summary>아이템 사용 — 지금은 상자 개봉뿐이다. 보유·개수 검증과 지급은 GachaService가 한다.</summary>
    [PacketHandler]
    public static void Handle_C_ItemUseRequest(ISession session, C_ItemUseRequest req)
    {
        ServerLog.Debug("상자", $"요청 ItemTID={req.ItemTID} Count={req.Count} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_ItemUseResponse { Result = EResultCode.NotLoggedIn, ItemTID = req.ItemTID });
            return;
        }

        GachaService.Instance.OpenBox(user, req.ItemTID, req.Count, DateTime.UtcNow);
    }

    /// <summary>특성 찍기. 조건(해금 행)·포인트 판정은 User가 한다.</summary>
    [PacketHandler]
    public static void Handle_C_UserTraitLearnRequest(ISession session, C_UserTraitLearnRequest req)
    {
        ServerLog.Debug("특성", $"요청 UserTraitTID={req.UserTraitTID} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_UserTraitLearnResponse { Result = EResultCode.NotLoggedIn, UserTraitTID = req.UserTraitTID });
            return;
        }

        user.TryLearnTrait(req.UserTraitTID, DateTime.UtcNow);
    }

    /// <summary>해금 요청. 조건 판정·차감은 User가 한다 — 클라가 "열 수 있다"고 그렸어도 여기서 다시 검사한다.</summary>
    [PacketHandler]
    public static void Handle_C_UnlockRequest(ISession session, C_UnlockRequest req)
    {
        ServerLog.Debug("해금", $"요청 UnlockTID={req.UnlockTID} 재화={req.Currency} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_UnlockResponse { Result = EResultCode.NotLoggedIn, UnlockTID = req.UnlockTID });
            return;
        }

        user.TryUnlock(req.UnlockTID, (GameData.CurrencyType)req.Currency, DateTime.UtcNow);
    }

    /// <summary>장비 장착. 보유·칸·종류 검증과 정산 순서는 User가 맡는다.</summary>
    [PacketHandler]
    public static void Handle_C_EquipRequest(ISession session, C_EquipRequest req)
    {
        ServerLog.Debug("장비", $"장착 요청 캐릭터={req.CharacterId} 장비={req.EquipId} 칸={req.Slot} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_EquipResponse { Result = EResultCode.NotLoggedIn, CharacterId = req.CharacterId, Slot = req.Slot });
            return;
        }

        user.TryEquip(req.CharacterId, req.EquipId, (GameData.EquipSlot)req.Slot, DateTime.UtcNow);
    }

    /// <summary>장비 해제.</summary>
    [PacketHandler]
    public static void Handle_C_UnequipRequest(ISession session, C_UnequipRequest req)
    {
        ServerLog.Debug("장비", $"해제 요청 캐릭터={req.CharacterId} 칸={req.Slot} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_EquipResponse { Result = EResultCode.NotLoggedIn, CharacterId = req.CharacterId, Slot = req.Slot });
            return;
        }

        user.TryUnequip(req.CharacterId, (GameData.EquipSlot)req.Slot, DateTime.UtcNow);
    }

    /// <summary>
    /// 아이템을 즉시 판매한다. <b>가격은 서버가 정한다</b> — 클라이언트는 무엇을 몇 개 팔지만 보낸다.
    /// </summary>
    [PacketHandler]
    public static void Handle_C_ItemSellRequest(ISession session, C_ItemSellRequest req)
    {
        ServerLog.Debug("상점", $"판매 요청 종류={req.Items?.Count ?? 0} sid={session.SessionId}");

        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_ItemSellResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        ShopService.Instance.Sell(user, req.Items);
    }

    /// <summary>우편 수령. MailId = 0이면 모두 받기. 창고 검사·지급은 User가 한다.</summary>
    [PacketHandler]
    public static void Handle_C_MailClaimRequest(ISession session, C_MailClaimRequest req)
    {
        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_MailClaimResponse { Result = EResultCode.NotLoggedIn });
            return;
        }

        user.TryClaimMail(req.MailId, DateTime.UtcNow);
    }

    /// <summary>받은 우편 삭제. 안 받은 우편은 거절한다.</summary>
    [PacketHandler]
    public static void Handle_C_MailDeleteRequest(ISession session, C_MailDeleteRequest req)
    {
        var user = session.GetUser();
        if (user == null)
        {
            session.SendPacket(new S_MailDeleteResponse { Result = EResultCode.NotLoggedIn, MailId = req.MailId });
            return;
        }

        user.TryDeleteMail(req.MailId);
    }
}
