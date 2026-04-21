# 모듈 서브타입 교체 (Module Swap)
# SubType 인코딩 / 기술레벨 게이팅 / 잠금해제 조건 / 자유 교체 규칙

## SubType 인코딩 구조
```
X  XX  XX
│   │   └───── model  (01=m1, 02=m2)
│   └───────── tech   (01=t1, 02=t2, 03=t3 ...)  ← 기술레벨 게이트
└───────────── type   (1=body, 2=beam, 3=missile, 4=hanger)
```
예시:
- `body_t1_m1 = 10101`  (tech=1)
- `body_t2_m1 = 10201`  (tech=2)
- `beam_t1_m1 = 20101`  (tech=1)

파싱: `type = val/10000`, `tech = (val/100)%100`, `model = val%100`
코드: `EModuleSubType.GetTechTier()` — `(val/10000)%100` 반환

---

## 신규 서브타입 잠금해제 조건 (AND, 3가지 모두 필요)

1. **기술레벨 ≥ 서브타입 tech tier**
   - t1 모듈 → 기술레벨 1 이상 (시작부터 가능)
   - t2 모듈 → 기술레벨 2 이상 필요
   - t3 모듈 → 기술레벨 3 이상 필요
   - **기술레벨 미달 시: 현재 모듈이 max level이어도 교체 불가**

2. **현재 장착 모듈 Lv.10 (max)**

3. **subTypeAddCost 납부 (슬롯 단위 최초 1회)**
   - std(t1) → adv(t2) 첫 잠금해제: **5,000 MR / 슬롯**
   - 이후 tier: 별도 정의 예정

---

## 자유 교체 (이미 잠금해제한 서브타입)
- 한 번이라도 subTypeAddCost를 납부한 서브타입은 이후 자유 교체 가능
- 조건 없음: 기술레벨 / 레벨 / 비용 모두 불필요
- 판단 기준: `ShipModuleLevel` 레코드 존재 여부 (별도 테이블 불필요)

---

## 교체 시 레벨 처리
- 신규 서브타입으로 교체 시 **Lv.1로 리셋**
- 이미 보유한 서브타입으로 재교체 시 레벨 유지 없음 (리셋 동일)
- adv max level이 std max level보다 높은 성능 → 레벨 리셋 감수할 가치 있음

---

## 관련 문서
- 기술레벨 조건: [tech_level/tech_level.md](../tech_level/tech_level.md)
- 레벨업 비용: [module_levelup.md](module_levelup.md)
