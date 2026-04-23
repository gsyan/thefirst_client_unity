# 게임 개요 및 재화 시스템
# 우주 함대 전투 시뮬레이션의 핵심 루프와 3종 자원(M/PM/TM) 구조 정의
# 자원 소모처: 함선 추가 / 슬롯 unlock / 모듈 레벨업 / 모듈 서브타입 추가 (소모 우선순위 M→PM→TM)

## 게임 개요
- 장르: 우주 함대 전투 시뮬레이션
- 핵심 루프: 존 탐험 → 자원 수집 → 함선 추가/모듈 강화 → 더 높은 존 탐험
- PvP 있음


## 재화 시스템

### 3종 자원
| 자원 | 약칭 | 획득처 | 만료 | 비고 |
|------|------|--------|------|------|
| Mineral | M | 존 탐사 클리어 보상 | 없음 (영구) | 기본 재화 |
| pvpMineral | PM | PVP 정산 — 서버 배치 자동 지급 | 있음 (기간 한정) | 기간 지난 M |
| tempMineral | TM | 현금 구매 (IAP) | 있음 (기간 한정) | 구매한 M |

- MineralRare / MineralExotic / MineralDark: **폐기**
- PM/TM은 "사용 기간이 있는 M" — 모든 소모처에서 M과 동일하게 사용
- 소모 우선순위: **M → PM(만료 임박 순) → TM(만료 임박 순)** — 서버 자동 처리
- PM 만료일: `CharacterInfo.pvpMineralExpiry` (ISO 8601), null이면 만료 없음
- TM 만료일: `CharacterInfo.tempMineralExpiry` (ISO 8601), null이면 만료 없음

### 자원 소모처 구조
1. 함선 추가          → addShipCosts (M, 부족 시 PM/TM 자동 보충)
2. 모듈 슬롯 unlock   → 1 고정/슬롯
3. 모듈 레벨업        → upgradeCost (M 단독, 부족 시 PM/TM 자동 보충)
4. 모듈 서브타입 추가  → 10 고정/슬롯 1회

### 투자와 환급 (리셋 시스템)
- 위 소모처는 모두 "투자"로 처리 — 각 슬롯/모듈에 3종 투자 이력을 서버 저장
- 리셋 시 투자 이력 기반으로 M/PM/TM 전액 환급, 수수료 없음
- 불변 항등식: `M_remain + Σ(all investedM) = 전체 스테이지 클리어로 획득한 M 총합`
  PM/TM도 동일 원칙 (각 풀 독립)
- 리셋 종류: 모듈 단위 리셋 / 함선 단위 리셋 → tab_ship.md 참고


## 기술레벨 시스템
- 목표 레벨: 2 / 4 / 6 / 8 (1차)
- 역할: 함선 추가 제한 조건, 최대 자원 보관량 결정 (시간 캡 × 시간당 수확량)
- 구현 완료: datatable_research.csv의 tech_level_N 노드 기반, FleetService.researchTechLevel 처리

## 자원 획득 방식
- M: 스테이지 클리어 시 즉시 지급 — 자동 적립 없음, 수확 버튼 없음
- PM: PVP 정산 시 서버 배치 자동 지급
- TM: 현금 구매(IAP) 즉시 지급
