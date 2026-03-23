# Settings 탭 UI
# 계정 관리, 환경 설정, 기타 정보를 섹션별로 구분한 설정 화면
# 개발자 도구는 UNITY_EDITOR || DEVELOPMENT_BUILD 조건부로만 표시

## 기능 목록

| 기능 | 설명 | 비고 |
|------|------|------|
| 지휘관명 변경 | 계정 내 캐릭터 이름 변경 팝업 | UIPopupRenameCharacter |
| 구글 계정 연동 | 게스트 계정 → 구글 연동 | 연동 후 버튼이 "연동 해제"로 전환 |
| 구글 연동 해제 | 구글 연동 → 게스트 상태로 복귀 | 서버에서 guestId 재발급 |
| 로그아웃 | 세션 종료 후 MainScene으로 이동 | 게스트: 계정 유실 위험, 구글: 단순 로그아웃 |
| 언어 변경 | Localization 언어 선택 드롭다운 | LocalizationManager 연동 |
| 외부 라이센스 | 서드파티 라이센스 목록 팝업 | UIPopupLicense |

---

## UI 레이아웃 (Editor 기준)

```
TabSettings (RectTransform, full stretch)
│
├── ScrollView (선택 사항 — 콘텐츠 길어질 때 대비)
│   └── Content (VerticalLayoutGroup, spacing=24, padding=40)
│
│       ├── Section_Account (계정)
│       │   ├── SectionHeader ("계정")          ← TMP, 작은 캡션
│       │   ├── Btn_RenameName                  ← 지휘관명 변경
│       │   ├── Btn_GoogleAccount               ← 구글 연동 / 연동 해제 (텍스트 동적)
│       │   └── Btn_Logout                      ← 빨간 계열 색상
│       │
│       ├── Separator (빈 공간 or HorizontalLine)
│       │
│       ├── Section_General (환경)
│       │   ├── SectionHeader ("환경")
│       │   └── Row_Language
│       │       ├── Label ("언어")
│       │       └── LanguageDropdown
│       │
│       ├── Separator
│       │
│       └── Section_Info (기타)
│           ├── SectionHeader ("기타")
│           └── Btn_License                     ← Third-Party Licenses
│
└── DevToolPanel (UNITY_EDITOR || DEVELOPMENT_BUILD 조건부)
    ├── Toggle_Mineral
    ├── Toggle_MineralRare
    ├── Toggle_MineralExotic
    ├── Toggle_MineralDark
    ├── Btn_TestMineral
    └── Toggle_RemoveAd
```

---

## 버튼 스타일 규칙

| 버튼 | 배경색 | 텍스트색 |
|------|--------|---------|
| 지휘관명 변경 | 기본 (회색) | 흰색 |
| 구글 계정 연동/해제 | 기본 (회색) | 흰색 |
| 로그아웃 | 어두운 빨강 (#7B1C1C) | 흰색 |
| 외부 라이센스 | 투명 or 매우 어두운 회색 | 회색 계열 |

- 모든 버튼 너비 통일 (preferredWidth: stretch or 고정 500px 등)
- 버튼 높이: 80px 내외 (2560×1440 기준)

---

## 개발자 도구 처리

- `DevToolPanel` GameObject는 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 없이 항상 오브젝트로 존재하되
- `UITabSettings.cs`에서 `SetActive` 조건 처리:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    m_devToolPanel.SetActive(true);
#else
    m_devToolPanel.SetActive(false);
#endif
```

- `[SerializeField] private GameObject m_devToolPanel;` 필드 추가 필요

---

## 연관 코드

- `Assets/Scripts/UI/UITab/UITabSettings.cs`
- `UIManager.ShowRenameCharacterPopup()`
- `UIManager.ShowLicensePopup()`
- `NetworkManager.LinkGoogle()` / `NetworkManager.UnlinkGoogle()`
- `LocalizationManager.SetLocale()`
