using System;
using System.Linq;
using MikaNetwork;
using MikaProtocol;

namespace MikaDummyClient
{
    public static class ServerPacketHandler
    {
        [PacketHandler]
        public static void Handle_S_EchoResponse(ISession session, S_EchoResponse res)
        {
            Console.WriteLine($"[Client] Recv Echo: {res.Message}");
        }

        [PacketHandler]
        public static void Handle_S_PongResponse(ISession session, S_PongResponse res)
        {
            Console.WriteLine("[Client] Recv Pong");
        }

        [PacketHandler]
        public static void Handle_S_LoginResponse(ISession session, S_LoginResponse res)
        {
            Console.WriteLine($"[Client] Recv Login: Result={res.Result}, SessionId={res.SessionId}");
        }

        [PacketHandler]
        public static void Handle_S_UpdateItemResponse(ISession session, S_UpdateItemResponse res)
        {
            Console.WriteLine($"[Client] Recv UpdateItem: Count={res.ItemChangeInfos?.Count}");
            foreach (var item in res.ItemChangeInfos!)
            {
                Console.WriteLine($"  - Kind={item.Kind.ToString()}, ItemId={item.ItemId}, Count={item.Count}");
            }
        }

        [PacketHandler]
        public static void Handle_S_InventoryResponse(ISession session, S_InventoryResponse res)
        {
            Console.WriteLine($"[Client] Recv Inventory: Count={res.Items?.Count}");
            foreach (var item in res.Items!)
            {
                Console.WriteLine($"  - ItemId={item.ItemId}, Count={item.Count}");
            }
        }

        [PacketHandler]
        public static void Handle_S_GachaDrawResponse(ISession session, S_GachaDrawResponse res)
        {
            if (res.Result != EResultCode.Ok)
            {
                Console.WriteLine($"[Client] Recv Gacha: 실패 Result={res.Result}");
                return;
            }

            Console.WriteLine($"[Client] Recv Gacha: Count={res.Rewards?.Count}");
            foreach (var reward in res.Rewards!)
            {
                // 종류에 따라 읽는 TID 필드가 다르다 — 반대쪽 필드는 항상 0이다.
                var tid = reward.RewardType == EGachaRewardType.Character ? reward.CharacterTid : reward.ItemId;
                Console.WriteLine($"  - {reward.RewardType} Rarity={reward.Rarity.ToString()}, Tid={tid}, Count={reward.Count}");
            }

            Console.WriteLine($"[Client] Recv Gacha 인벤토리 변경: Count={res.ItemChangeInfos?.Count}");
            foreach (var change in res.ItemChangeInfos!)
            {
                Console.WriteLine($"  - Kind={change.Kind.ToString()}, ItemId={change.ItemId}, Count={change.Count} (누적 총량)");
            }
        }

        [PacketHandler]
        public static void Handle_S_WorkStationSlotsResponse(ISession session, S_WorkStationSlotsResponse res)
        {
            Console.WriteLine($"[Client] Recv 작업슬롯: Count={res.Slots?.Count}");
            foreach (var slot in res.Slots!)
            {
                Console.WriteLine($"  - Slot={slot.SlotIndex}, Industry={slot.Industry}, " +
                                  $"Character={slot.CharacterId}, LastTick={slot.LastTickAtUnixMs}");
            }
        }

        [PacketHandler]
        public static void Handle_S_WorkStationAssignResponse(ISession session, S_WorkStationAssignResponse res)
        {
            if (res.Result != EResultCode.Ok)
            {
                Console.WriteLine($"[Client] Recv 슬롯 배치: 실패 Result={res.Result}");
                return;
            }

            Console.WriteLine($"[Client] Recv 슬롯 배치: Slot={res.Slot?.SlotIndex}, " +
                              $"Industry={res.Slot?.Industry}, Character={res.Slot?.CharacterId}");
        }

        // 서버가 요청 없이 밀어 주는 채취 결과. 클라이언트는 받기만 한다(서버 권위).
        [PacketHandler]
        public static void Handle_S_GatherResultResponse(ISession session, S_GatherResultResponse res)
        {
            Console.WriteLine($"[Client] Recv 채취: Slot={res.SlotIndex}, 판정={res.JudgeCount}회");
            foreach (var change in res.ItemChanges!)
            {
                Console.WriteLine($"  - ItemId={change.ItemId}, Count={change.Count}, Kind={change.Kind}");
            }
        }

        [PacketHandler]
        public static void Handle_S_WorkStationSlotSyncResponse(ISession session, S_WorkStationSlotSyncResponse res)
        {
            Console.WriteLine($"[Client] Recv 슬롯 동기화: Slot={res.Slot?.SlotIndex}, " +
                              $"Speed={res.Slot?.CurrentWorkSpeed}, LastTick={res.Slot?.LastTickAtUnixMs}");
        }

        [PacketHandler]
        public static void Handle_S_CharacterListResponse(ISession session, S_CharacterListResponse res)
        {
            Console.WriteLine($"[Client] Recv 캐릭터: Count={res.Characters?.Count}");
            foreach (var character in res.Characters!)
            {
                var aptitudes = string.Join(" ", character.Aptitudes.Select(a => $"{a.Industry}={a.Value}"));
                Console.WriteLine($"  - Id={character.CharacterId}, Tid={character.CharacterTid}, " +
                                  $"Lv={character.Level}, Exp={character.Exp}, 적성: {aptitudes}");
            }
        }

        [PacketHandler]
        public static void Handle_S_CharacterSyncResponse(ISession session, S_CharacterSyncResponse res)
        {
            Console.WriteLine($"[Client] Recv 캐릭터 동기화: Id={res.Character?.CharacterId}, " +
                              $"Lv={res.Character?.Level}, Exp={res.Character?.Exp}");
        }

        [PacketHandler]
        public static void Handle_S_CheatResponse(ISession session, S_CheatResponse res)
        {
            Console.WriteLine($"[Client] Recv 치트: {res.Command} → {res.Result} {res.Message}");
        }

        [PacketHandler]
        public static void Handle_S_AptitudeUpResponse(ISession session, S_AptitudeUpResponse res)
        {
            var c = res.Character;
            Console.WriteLine($"[Client] Recv 적성 찍기: {res.Result} Id={c?.CharacterId} 남은 포인트={c?.AptitudePoints}");
        }

        [PacketHandler]
        public static void Handle_S_UnlockResponse(ISession session, S_UnlockResponse res)
        {
            Console.WriteLine($"[Client] Recv 해금: UnlockTID={res.UnlockTID} → {res.Result}");
        }

        // 특성 찍기 결과. 성공이면 앞에 S_UnlockResponse가, 뒤에 S_AccountLevelResponse(남은 포인트)가 온다.
        [PacketHandler]
        public static void Handle_S_UserTraitLearnResponse(ISession session, S_UserTraitLearnResponse res)
        {
            Console.WriteLine($"[Client] Recv 특성: UserTraitTID={res.UserTraitTID} → {res.Result}");
        }

        // 로그인 직후·경험치가 오를 때·포인트를 쓸 때 온다.
        [PacketHandler]
        public static void Handle_S_AccountLevelResponse(ISession session, S_AccountLevelResponse res)
        {
            Console.WriteLine($"[Client] Recv 계정: Lv{res.Level} Exp={res.Exp} 특성포인트={res.TraitPoint}");
        }

        // 로그인 직후 슬롯 스냅샷보다 먼저 온다. 잠긴 칸의 조건 문구는 UnlockTable로 클라가 만든다.
        [PacketHandler]
        public static void Handle_S_UnlockListResponse(ISession session, S_UnlockListResponse res)
        {
            Console.WriteLine($"[Client] Recv 열린 해금: {string.Join(", ", res.UnlockTIDs)}");
        }

        // 로그인 직후 캐릭터 목록 뒤·슬롯 스냅샷 앞. EquippedCharacterId=0이면 창고.
        [PacketHandler]
        public static void Handle_S_EquipListResponse(ISession session, S_EquipListResponse res)
        {
            Console.WriteLine($"[Client] Recv 보유 장비 {res.Equips.Count}개: " +
                string.Join(", ", res.Equips.Select(e => $"#{e.EquipId}(TID {e.EquipTid})→{e.EquippedCharacterId}/{e.EquippedSlot} 칸{e.SlotPosition}")));
        }

        // 바뀐 개체만 온다 — 지급·장착·해제·자동 이동. EquipId로 덮어쓴다.
        [PacketHandler]
        public static void Handle_S_EquipSyncResponse(ISession session, S_EquipSyncResponse res)
        {
            Console.WriteLine($"[Client] Recv 장비 동기화: " +
                string.Join(", ", res.Equips.Select(e => $"#{e.EquipId}(TID {e.EquipTid})→{e.EquippedCharacterId}/{e.EquippedSlot} 칸{e.SlotPosition}")));
        }

        [PacketHandler]
        public static void Handle_S_EquipResponse(ISession session, S_EquipResponse res)
        {
            Console.WriteLine($"[Client] Recv 장비 {res.Slot} @캐릭터 {res.CharacterId} → {res.Result}");
        }

        // 로그인 스냅샷과 변경 푸시가 같은 패킷으로 온다.
        // 증감이 아니라 확정 잔액이라 두 경우 모두 덮어쓰기로 처리하면 된다.
        [PacketHandler]
        public static void Handle_S_CurrencyResponse(ISession session, S_CurrencyResponse res)
        {
            Console.WriteLine($"[Client] Recv 재화: Gold={res.Gold}, Dia={res.Dia}");
        }

        // 잔액은 이 패킷이 아니라 뒤따르는 S_CurrencyResponse가 들고 온다(GainedGold는 이번에 번 금액).
        [PacketHandler]
        public static void Handle_S_ItemSellResponse(ISession session, S_ItemSellResponse res)
        {
            if (res.Result != EResultCode.Ok)
            {
                Console.WriteLine($"[Client] Recv 판매: 실패 Result={res.Result}");
                return;
            }

            Console.WriteLine($"[Client] Recv 판매: 획득 골드={res.GainedGold}, 변경={res.ItemChangeInfos?.Count}");
            foreach (var item in res.ItemChangeInfos!)
            {
                Console.WriteLine($"  - Kind={item.Kind.ToString()}, ItemId={item.ItemId}, Count={item.Count}");
            }
        }
    }
}

