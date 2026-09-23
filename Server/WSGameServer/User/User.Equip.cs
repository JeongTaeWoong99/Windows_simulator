using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>보유 장비 개체. 키는 개체 PK(<c>t_user_equip.equip_id</c>)다.</summary>
    private readonly Dictionary<long, Equip> _equips = new();

    /// <summary>착용 매핑 (캐릭터, 칸) → 장비. <c>t_character_equip</c>의 메모리 사본이며 한 칸에 하나다.</summary>
    private readonly Dictionary<(long CharacterId, EquipSlot Slot), Equip> _worn = new();

    /// <summary>지급을 요청했지만 아직 PK가 돌아오지 않은 창고 칸. 연속 지급이 같은 칸을 고르지 않게 한다.</summary>
    private readonly HashSet<int> _pendingEquipPositions = new();

    public IReadOnlyCollection<Equip> Equips => _equips.Values;

    public bool TryGetEquip(long equipId, out Equip equip)
        => _equips.TryGetValue(equipId, out equip!);

    /// <summary>DB에서 읽은 개체·매핑을 적재한다(로그인 시 1회). 캐릭터 적재 뒤, 작업슬롯 적재 앞이다 — 슬롯 속도의 근거다.</summary>
    public void LoadEquips(IReadOnlyList<UserEquipRow> equipRows, IReadOnlyList<CharacterEquipRow> wornRows)
    {
        _equips.Clear();
        _worn.Clear();

        foreach (var r in equipRows)
        {
            // 테이블에 없는 TID는 건너뛴다 — 데이터 한 줄 때문에 로그인이 막히면 안 된다.
            if (!_equipCatalog.TryGet(r.equip_tid, out var row))
            {
                ServerLog.Warn("로그인", $"EquipTable에 없는 TID, 건너뜀: {r.equip_tid} (개체 {r.equip_id})");
                continue;
            }

            var equip = new Equip(r.equip_id, row, r.slot_position);

            if (r.enchant_grade != 0)
            {
                // 테이블에 없는 EnchantOptionTID는 그 줄만 버린다 — 데이터 한 줄 때문에 로그인이 막히면 안 된다.
                var options = new List<EnchantOptionTableRow>();
                foreach (var tid in new[] { r.enchant_1, r.enchant_2, r.enchant_3 })
                {
                    if (tid == 0)
                    {
                        continue;
                    }
                    if (!_enchantCatalog.TryGetOption(tid, out var option))
                    {
                        ServerLog.Warn("로그인", $"EnchantOptionTable에 없는 EnchantOptionTID, 건너뜀: {tid} (개체 {r.equip_id})");
                        continue;
                    }
                    options.Add(option);
                }

                equip.SetEnchant((GlobalRarity)r.enchant_grade, options);
            }

            _equips[r.equip_id] = equip;
        }

        foreach (var r in wornRows)
        {
            var slot = (EquipSlot)r.slot;
            if (!_equips.TryGetValue(r.equip_id, out var equip) || !_characters.ContainsKey(r.character_id) || !EquipCatalog.IsValidSlot(slot))
            {
                ServerLog.Warn("로그인", $"착용 매핑이 가리키는 대상이 없어 건너뜀: 캐릭터 {r.character_id} 칸 {r.slot} 장비 {r.equip_id}");
                continue;
            }

            equip.Wear(r.character_id, slot);
            _worn[(r.character_id, slot)] = equip;
        }
    }

    /// <summary>보유 장비 전체 스냅샷(로그인 직후). 캐릭터 목록 뒤·슬롯 스냅샷 앞.</summary>
    public void SendEquipList()
    {
        Send(new S_EquipListResponse { Equips = _equips.Values.Select(ToEquipInfo).ToList() });
    }

    /// <summary>
    /// 장착. 검증 → <b>정산</b> → 매핑 갱신(이전 착용자 해제 · 같은 칸 장비 창고로) → 속도 재확정 → 저장(트랜잭션) → 응답·싱크.
    /// 정산이 매핑보다 먼저여야 새 속도가 이전 구간에 소급되지 않는다 → Server/docs/채취-정산.md 3장
    /// </summary>
    public void TryEquip(long characterId, long equipId, EquipSlot slot, DateTime now)
    {
        if (!TryGetCharacter(characterId, out _))
        {
            Reject(EResultCode.CharacterNotOwned, "미보유 캐릭터");
            return;
        }

        if (!TryGetEquip(equipId, out var equip))
        {
            Reject(EResultCode.EquipNotOwned, $"미보유 장비 {equipId}");
            return;
        }

        if (!EquipCatalog.IsValidSlot(slot))
        {
            Reject(EResultCode.InvalidEquipSlot, "칸 범위 밖");
            return;
        }

        if (!EquipCatalog.CanEquip(equip.Kind, slot))
        {
            Reject(EResultCode.EquipKindMismatch, $"{equip.Kind}는 {slot}에 못 낀다");
            return;
        }

        if (equip.EquippedCharacterId == characterId && equip.EquippedSlot == slot)
        {
            Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
            return;
        }

        SettleWorkStation(now);

        var changed = new List<Equip>();
        var changes = new List<EquipMappingChange>();

        // 1) 이 장비가 다른 곳에 있었으면 그 칸을 비운다.
        if (equip.IsEquipped)
        {
            _worn.Remove((equip.EquippedCharacterId, equip.EquippedSlot));
            changes.Add(new EquipMappingChange(equip.EquippedCharacterId, equip.EquippedSlot, 0));
        }

        // 2) 대상 칸에 있던 장비는 창고로. 그 칸의 DB 행은 3)의 변경이 덮는다.
        if (_worn.TryGetValue((characterId, slot), out var displaced))
        {
            displaced.TakeOff();
            changed.Add(displaced);
        }

        // 3) 새 매핑
        equip.Wear(characterId, slot);
        _worn[(characterId, slot)] = equip;
        changed.Add(equip);
        changes.Add(new EquipMappingChange(characterId, slot, equipId));

        RefreshWorkStationSpeed(now);
        PostDBTask(new SaveCharacterEquipRepository(this, changes));

        ServerLog.Info("장비", $"장착 Uid={Uid} 캐릭터 {characterId} {slot} ← 장비 {equipId}(TID {equip.Tid})");
        Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
        Send(new S_EquipSyncResponse { Equips = changed.Select(ToEquipInfo).ToList() });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("장비", $"장착 거절 — {reason}. Uid={Uid} 캐릭터 {characterId} 장비 {equipId} 칸 {slot}");
            Send(new S_EquipResponse { Result = code, CharacterId = characterId, Slot = (EEquipSlot)slot });
        }
    }

    /// <summary>해제. 정산 → 매핑 제거 → 속도 재확정 → 저장 → 응답·싱크. 순서 이유는 <see cref="TryEquip"/>과 같다.</summary>
    public void TryUnequip(long characterId, EquipSlot slot, DateTime now)
    {
        if (!TryGetCharacter(characterId, out _))
        {
            Reject(EResultCode.CharacterNotOwned, "미보유 캐릭터");
            return;
        }

        if (!EquipCatalog.IsValidSlot(slot))
        {
            Reject(EResultCode.InvalidEquipSlot, "칸 범위 밖");
            return;
        }

        if (!_worn.TryGetValue((characterId, slot), out var equip))
        {
            Reject(EResultCode.EquipSlotEmpty, "빈 칸");
            return;
        }

        SettleWorkStation(now);

        equip.TakeOff();
        _worn.Remove((characterId, slot));

        RefreshWorkStationSpeed(now);
        PostDBTask(new SaveCharacterEquipRepository(this, new[] { new EquipMappingChange(characterId, slot, 0) }));

        ServerLog.Info("장비", $"해제 Uid={Uid} 캐릭터 {characterId} {slot} → 장비 {equip.Id}");
        Send(new S_EquipResponse { Result = EResultCode.Ok, CharacterId = characterId, Slot = (EEquipSlot)slot });
        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("장비", $"해제 거절 — {reason}. Uid={Uid} 캐릭터 {characterId} 칸 {slot}");
            Send(new S_EquipResponse { Result = code, CharacterId = characterId, Slot = (EEquipSlot)slot });
        }
    }

    /// <summary>이 캐릭터가 착용한 장비 중 이 산업에 붙는 가산의 합(천분율). 미배치(0)면 0.</summary>
    public int GetEquipSpeedAdd(long characterId, IndustryType industry)
    {
        if (characterId == 0)
        {
            return 0;
        }

        var sum = 0;
        foreach (var ((wornBy, _), equip) in _worn)
        {
            if (wornBy == characterId && equip.AppliesTo(industry))
            {
                sum += equip.SpeedAddPermille;
            }
        }

        return sum;
    }

    /// <summary>창고 장비 탭의 첫 빈 칸(0부터). 지급 대기 중인 칸도 찬 것으로 본다.</summary>
    public int NextFreeEquipPosition()
    {
        var used = new HashSet<int>(_pendingEquipPositions);
        foreach (var equip in _equips.Values)
        {
            used.Add(equip.SlotPosition);
        }

        var position = 0;
        while (used.Contains(position))
        {
            position++;
        }

        return position;
    }

    /// <summary>장비 개체 1개를 지급한다(치트·앞으로의 획득 경로가 같은 길을 쓴다). 개체 PK는 <see cref="OnEquipGranted"/>로 돌아온다.</summary>
    /// <returns>지급을 요청했으면 true. 테이블에 없는 TID면 false.</returns>
    public bool GrantEquip(int equipTid)
    {
        if (!_equipCatalog.TryGet(equipTid, out _))
        {
            ServerLog.Warn("장비", $"지급 거절 — EquipTable에 없는 TID {equipTid}. Uid={Uid}");
            return false;
        }

        var position = NextFreeEquipPosition();
        _pendingEquipPositions.Add(position);
        PostDBTask(new GrantEquipRepository(this, equipTid, position));
        return true;
    }

    /// <summary>지급이 끝나면 불린다(로직 스레드). 메모리에 올리고 그 개체만 밀어 준다.</summary>
    public void OnEquipGranted(long equipId, int equipTid, int slotPosition)
    {
        _pendingEquipPositions.Remove(slotPosition);

        if (!_equipCatalog.TryGet(equipTid, out var row))
        {
            ServerLog.Warn("장비", $"EquipTable에 없는 TID, 적재 건너뜀: {equipTid} (개체 {equipId})");
            return;
        }

        var equip = new Equip(equipId, row, slotPosition);
        _equips[equipId] = equip;

        ServerLog.Info("장비", $"지급 Uid={Uid} 장비 {equipId}(TID {equipTid}) 칸 {slotPosition}");
        Send(new S_EquipSyncResponse { Equips = new List<EquipInfo> { ToEquipInfo(equip) } });
    }

    private static EquipInfo ToEquipInfo(Equip e)
    {
        return new EquipInfo
        {
            EquipId             = e.Id,
            EquipTid            = e.Tid,
            EquippedCharacterId = e.EquippedCharacterId,
            EquippedSlot        = (EEquipSlot)e.EquippedSlot,
            SlotPosition        = e.SlotPosition,
            EnchantGrade        = (int)e.EnchantGrade,
            EnchantOptions      = e.EnchantOptionTids.ToList(),
        };
    }
}
