# 모듈 슬롯 Unlock

## 데이터
- `DataTableConfig.cs` — `moduleUnlockPrice`: 슬롯 unlock 고정 비용

## 규칙
- 비용: M 고정, 슬롯 타입 무관, 슬롯별 독립
- unlock 즉시 해당 타입의 기본 서브타입(t1_m1) Lv.1 모듈 자동 장착
- 내부: placeholder → 실제 모듈 교체 (`SpaceShip.Apply_UnlockModule`)

## 초기 함선 기본 unlock 상태
- beam 슬롯 1개 unlock된 상태로 지급
- 나머지 슬롯(missile, hanger)은 잠금(placeholder) 상태
