using System;
using System.Collections.Generic;
using System.Linq;
using MikaProtocol;

namespace MikaDummyClient
{
    /// <summary>
    /// 보낼 패킷을 번호로 선택하고 필요한 필드를 입력받아 송신하는 메뉴 루프.
    /// 액션을 리스트에 등록하면 번호가 자동으로 부여된다(레지스트리 방식).
    /// 새 패킷을 시험하려면 <see cref="_actions"/>에 항목 하나만 추가하면 된다.
    /// </summary>
    public sealed class PacketMenu
    {
        private readonly List<ClientAction> _actions;

        public PacketMenu()
        {
            _actions = new List<ClientAction>
            {
                new ClientAction("Echo (채팅)", SendEcho),
                new ClientAction("Ping", SendPing),
                new ClientAction("Login", SendLogin),
                new ClientAction("AddItem", SendAddItem),
                new ClientAction("GachaDraw", SendGachaDraw),
                new ClientAction("WorkStationAssign (슬롯 배치)", SendWorkStationAssign),
                new ClientAction("ItemSell (즉시 판매)", SendItemSell),
                new ClientAction("Cheat (admin 전용 — 지급·정산)", SendCheat),
                new ClientAction("Unlock (해금 — 작업슬롯 1002~1007)", SendUnlock),
                new ClientAction("Equip (장착 — 캐릭터ID 장비ID 칸1~4)", SendEquip),
                new ClientAction("Unequip (해제 — 캐릭터ID 칸1~4)", SendUnequip),
                new ClientAction("EquipEnchant (인챈트 — 장비ID 아이템TID 100013~100017)", SendEquipEnchant),
                new ClientAction("UserTraitLearn (특성 찍기 — 산업 레벨 2xxx · 속도 3xxx)", SendUserTraitLearn),
                new ClientAction("ItemUse (상자 열기 — 100007 나무 · 100008 은 · 100009 황금)", SendItemUse),
                new ClientAction("MailClaim (우편 수령 — MailId, 0이면 모두 받기)", SendMailClaim),
                new ClientAction("MailDelete (받은 우편 삭제 — MailId)", SendMailDelete),
                new ClientAction("AuctionSearch (경매 검색 — 종류 0전체/1자원/2장비 · TID 목록)", SendAuctionSearch),
                new ClientAction("AuctionRegister (경매 등록 — 자원 TID·수량 또는 장비ID · 단가)", SendAuctionRegister),
                new ClientAction("AuctionBuy (경매 구매 — 매물ID · 본 총액)", SendAuctionBuy),
                new ClientAction("AuctionCancel (경매 취소 — 매물ID)", SendAuctionCancel),
                new ClientAction("AuctionMyListings (내 매물)", SendAuctionMyListings),
                new ClientAction("MarketItems (거래소 목록 — TID 목록, 비우면 전체)", SendMarketItems),
                new ClientAction("MarketPrice (거래소 가격대 — TID)", SendMarketPrice),
                new ClientAction("MarketBuy (거래소 구매 — TID · 수량 · 단가 상한)", SendMarketBuy),
            };
        }

        /// <summary>
        /// 메뉴를 반복 표시하며 번호 입력을 처리한다. 0을 입력하면 종료한다.
        /// </summary>
        public void Run()
        {
            while (true)
            {
                PrintMenu();

                Console.Write("Select > ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (!int.TryParse(input.Trim(), out int choice))
                {
                    Console.WriteLine("[Client] 숫자를 입력하세요.\n");
                    continue;
                }

                if (choice == 0)
                {
                    break;
                }

                if (choice < 1 || choice > _actions.Count)
                {
                    Console.WriteLine("[Client] 존재하지 않는 번호입니다.\n");
                    continue;
                }

                _actions[choice - 1].Execute();
                Console.WriteLine();
            }

            Console.WriteLine("[Client] 서버와 연결을 해제하고 종료합니다.");
        }

        private void PrintMenu()
        {
            Console.WriteLine("=== 보낼 패킷 선택 ===");
            for (int i = 0; i < _actions.Count; i++)
            {
                Console.WriteLine($"{i + 1}) {_actions[i].Label}");
            }
            Console.WriteLine("0) 종료");
        }

        // --- 각 패킷 액션 ---

        private void SendEcho()
        {
            Console.Write("보낼 메시지 > ");
            string message = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_EchoRequest { Message = message });
        }

        private void SendPing()
        {
            NetworkManager.Instance.Send(new C_PingRequest());
        }

        private void SendLogin()
        {
            Console.Write("로그인 Id > ");
            string id = Console.ReadLine() ?? "";
            NetworkManager.Instance.Send(new C_LoginRequest { Id = id });
        }

        private void SendAddItem()
        {
            Console.Write("ItemId > ");
            if (!int.TryParse(Console.ReadLine(), out int itemId))
            {
                Console.WriteLine("[Client] ItemId는 숫자여야 합니다.");
                return;
            }

            Console.Write("Count > ");
            if (!int.TryParse(Console.ReadLine(), out int count))
            {
                Console.WriteLine("[Client] Count는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_AddItemRequest { ItemId = itemId, Count = count });
        }

        private void SendWorkStationAssign()
        {
            Console.Write("SlotIndex (기본 0) > ");
            string? slotInput = Console.ReadLine();
            int slotIndex = string.IsNullOrWhiteSpace(slotInput) ? 0 : int.Parse(slotInput.Trim());

            // GameData.ItemType — 1=농사 2=낚시 3=채굴 4=벌목 5=사냥 (0=해제)
            Console.Write("Industry (2=낚시) > ");
            if (!byte.TryParse(Console.ReadLine(), out byte industry))
            {
                Console.WriteLine("[Client] Industry는 숫자여야 합니다.");
                return;
            }

            Console.Write("CharacterId (0=해제) > ");
            if (!long.TryParse(Console.ReadLine(), out long characterId))
            {
                Console.WriteLine("[Client] CharacterId는 숫자여야 합니다.");
                return;
            }

            // 빈칸이면 0 — 서버가 기본 레벨(Lv1)로 본다. 상위 레벨은 특성으로 열어야 통과한다.
            Console.Write("IndustryLevel (1~5, 기본 1) > ");
            byte.TryParse(Console.ReadLine(), out byte industryLevel);

            NetworkManager.Instance.Send(new C_WorkStationAssignRequest
            {
                SlotIndex = slotIndex, Industry = (EIndustryType)industry, CharacterId = characterId, IndustryLevel = industryLevel,
            });
        }

        private void SendUnlock()
        {
            Console.Write("UnlockTID (작업슬롯 2~7번 칸 = 1002~1007) > ");
            if (!int.TryParse(Console.ReadLine(), out int unlockTid))
            {
                Console.WriteLine("[Client] UnlockTID는 숫자여야 합니다.");
                return;
            }

            // 지불 컬럼이 없는 해금은 서버가 재화를 무시한다. 빈칸이면 골드.
            Console.Write("Currency (1=골드 2=다이아, 기본 1) > ");
            string? currencyInput = Console.ReadLine();
            byte currency = string.IsNullOrWhiteSpace(currencyInput) ? (byte)1 : byte.Parse(currencyInput.Trim());

            NetworkManager.Instance.Send(new C_UnlockRequest { UnlockTID = unlockTid, Currency = (ECurrencyType)currency });
        }

        private void SendEquip()
        {
            Console.Write("CharacterId EquipId Slot(1무기 2·3장신구 4보석) > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3 || !long.TryParse(parts[0], out var characterId) || !long.TryParse(parts[1], out var equipId) || !byte.TryParse(parts[2], out var slot))
            {
                Console.WriteLine("[Client] 숫자 세 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_EquipRequest { CharacterId = characterId, EquipId = equipId, Slot = (EEquipSlot)slot });
        }

        private void SendUnequip()
        {
            Console.Write("CharacterId Slot(1~4) > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var characterId) || !byte.TryParse(parts[1], out var slot))
            {
                Console.WriteLine("[Client] 숫자 두 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_UnequipRequest { CharacterId = characterId, Slot = (EEquipSlot)slot });
        }

        // 무엇을 하는지는 아이템이 정한다(EnchantItemTable) — 동작을 따로 고르지 않는다.
        private void SendEquipEnchant()
        {
            Console.Write("EquipId ItemTID > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[0], out var equipId) || !int.TryParse(parts[1], out var itemTid))
            {
                Console.WriteLine("[Client] 숫자 두 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_EquipEnchantRequest { EquipId = equipId, ItemTid = itemTid });
        }

        private void SendCheat()
        {
            Console.WriteLine("명령: 1=GiveGold(Arg1=금액, 음수면 차감) 2=GiveDia 3=GiveItem(TID, 개수) " +
                              "4=GiveCharacter(TID, 장수) 5=GiveCharacterExp(개체Id, 경험치) 6=Settle(판정 횟수) 7=Unlock(UnlockTID) " +
                              "8=GiveEquip(EquipTID) 9=GiveAccountExp(경험치) 10=SendMail(템플릿TID, 받는 UID — 0이면 전체)");
            Console.Write("Command > ");
            if (!byte.TryParse(Console.ReadLine(), out byte command))
            {
                Console.WriteLine("[Client] Command는 숫자여야 합니다.");
                return;
            }

            Console.Write("Arg1 (없으면 빈칸) > ");
            long.TryParse(Console.ReadLine(), out long arg1);
            Console.Write("Arg2 (없으면 빈칸) > ");
            long.TryParse(Console.ReadLine(), out long arg2);

            NetworkManager.Instance.Send(new C_CheatRequest { Command = (ECheatCommand)command, Arg1 = arg1, Arg2 = arg2 });
        }

        private void SendMailClaim()
        {
            Console.Write("MailId (0 = 모두 받기) > ");
            long.TryParse(Console.ReadLine(), out long mailId);
            NetworkManager.Instance.Send(new C_MailClaimRequest { MailId = mailId });
        }

        private void SendMailDelete()
        {
            Console.Write("MailId > ");
            long.TryParse(Console.ReadLine(), out long mailId);
            NetworkManager.Instance.Send(new C_MailDeleteRequest { MailId = mailId });
        }

        private void SendItemUse()
        {
            Console.Write("ItemTID Count(1~99) > ");
            var parts = (Console.ReadLine() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var itemTid) || !int.TryParse(parts[1], out var count))
            {
                Console.WriteLine("[Client] 숫자 두 개를 띄어 적습니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_ItemUseRequest { ItemTID = itemTid, Count = count });
        }

        private void SendUserTraitLearn()
        {
            Console.Write("UserTraitTID (산업 레벨 = 2000+산업×100+레벨 · 속도 = 3000+산업×100+단) > ");
            if (!int.TryParse(Console.ReadLine(), out int userTraitTid))
            {
                Console.WriteLine("[Client] UserTraitTID는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_UserTraitLearnRequest { UserTraitTID = userTraitTid });
        }

        private void SendGachaDraw()
        {
            Console.Write("GachaId (1=아이템 · 2=캐릭터 · 3=무기 · 4=장신구 · 5=보석) > ");
            string? gachaInput = Console.ReadLine();
            int gachaId = string.IsNullOrWhiteSpace(gachaInput) ? 1 : int.Parse(gachaInput.Trim());

            Console.Write("DrawCount (1 또는 10) > ");
            if (!int.TryParse(Console.ReadLine(), out int drawCount))
            {
                Console.WriteLine("[Client] DrawCount는 숫자여야 합니다.");
                return;
            }

            NetworkManager.Instance.Send(new C_GachaDrawRequest { GachaId = gachaId, DrawCount = drawCount });
        }

        private void SendItemSell()
        {
            // 일괄 판매가 기본 동선이라 목록으로 받는다 — "10001:5, 10002:3"
            Console.Write("판매 목록 (ItemId:Count, 쉼표 구분) > ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("[Client] 판매 목록이 비어 있습니다.");
                return;
            }

            var items = new List<ItemInfo>();
            foreach (string pair in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split(':');
                if (parts.Length != 2
                    || !int.TryParse(parts[0].Trim(), out int itemId)
                    || !int.TryParse(parts[1].Trim(), out int count))
                {
                    Console.WriteLine($"[Client] 형식이 잘못됐습니다: {pair} (ItemId:Count)");
                    return;
                }

                items.Add(new ItemInfo { ItemId = itemId, Count = count });
            }

            NetworkManager.Instance.Send(new C_ItemSellRequest { Items = items });
        }
            private static long ReadLong(string prompt)
        {
            Console.Write(prompt);
            return long.TryParse(Console.ReadLine(), out var value) ? value : 0;
        }

        // 이름 검색은 클라가 이름을 TID로 바꿔 보낸다 — 더미는 TID를 직접 받는다(쉼표 구분, 비우면 전체).
        private void SendAuctionSearch()
        {
            var kind = (EAuctionKind)ReadLong("종류 (0=전체 1=자원 2=장비) > ");
            Console.Write("TID 목록 (쉼표, 비우면 전체) > ");
            var tids = (Console.ReadLine() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
            NetworkManager.Instance.Send(new C_AuctionSearchRequest { Kind = kind, Tids = tids });
        }

        private void SendAuctionRegister()
        {
            var kind = (EAuctionKind)ReadLong("종류 (1=자원 2=장비) > ");
            if (kind == EAuctionKind.Equip)
            {
                var equipId = ReadLong("장비ID > ");
                NetworkManager.Instance.Send(new C_AuctionRegisterRequest { Kind = kind, EquipId = equipId, UnitPrice = ReadLong("단가 > ") });
                return;
            }

            var itemTid = (int)ReadLong("ItemTID > ");
            var count   = (int)ReadLong("수량 > ");
            NetworkManager.Instance.Send(new C_AuctionRegisterRequest { Kind = kind, ItemTid = itemTid, Count = count, UnitPrice = ReadLong("단가 > ") });
        }

        private void SendAuctionBuy()
        {
            var listingId = ReadLong("매물ID > ");
            NetworkManager.Instance.Send(new C_AuctionBuyRequest { ListingId = listingId, ExpectedTotalPrice = ReadLong("본 총액 > ") });
        }

        private void SendAuctionCancel()
        {
            NetworkManager.Instance.Send(new C_AuctionCancelRequest { ListingId = ReadLong("매물ID > ") });
        }

        private void SendAuctionMyListings()
        {
            NetworkManager.Instance.Send(new C_AuctionMyListingsRequest());
        }
        private void SendMarketItems()
        {
            Console.Write("TID 목록 (쉼표, 비우면 전체) > ");
            var tids = (Console.ReadLine() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
            NetworkManager.Instance.Send(new C_MarketItemsRequest { Tids = tids });
        }

        private void SendMarketPrice()
        {
            NetworkManager.Instance.Send(new C_MarketPriceRequest { Tid = (int)ReadLong("TID > ") });
        }

        private void SendMarketBuy()
        {
            var tid   = (int)ReadLong("TID > ");
            var count = (int)ReadLong("수량 > ");
            NetworkManager.Instance.Send(new C_MarketBuyRequest { Tid = tid, Count = count, MaxUnitPrice = ReadLong("단가 상한 > ") });
        }
}
}
