# 모듈 서브타입 교체 (Module Swap)
# SubType 인코딩 / 기술레벨 게이팅 / 잠금해제 조건

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

## 교체 규칙
- 교체는 **단방향 선형**만 가능 (t1→t2→t3... 순서)
- 역방향 불가, 건너뛰기 불가
- 교체 후 레벨 리셋: 신규 서브타입 Lv.1로 시작

## 서브타입 교체 조건 (AND, 3가지 모두 필요)

1. **현재 장착 모듈 = 교체 대상의 prerequisite 서브타입 + Lv.10 (max)**
   - t2로 교체하려면 현재 t1 장착 + t1 Lv.10
   - t3로 교체하려면 현재 t2 장착 + t2 Lv.10

2. **기술레벨 ≥ 서브타입 tech tier**
   - t2 교체: 기술레벨 2 이상 필요
   - t3 교체: 기술레벨 3 이상 필요

3. **비용 납부** (`datatable_research_subtype.csv` 기준)
   - 비용은 모듈 타입(body/beam/missile/hanger)별 독립
   - t2부터 M 소모, 중반 이후 MR/ME/MD 추가

---

## 관련 문서
- 기술레벨 조건: [tech_level/tech_level.md](../tech_level/tech_level.md)
- 레벨업 비용: [module_levelup.md](module_levelup.md)
