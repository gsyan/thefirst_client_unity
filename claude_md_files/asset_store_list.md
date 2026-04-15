## 함선 모듈 에셋
### USSN Freedom Class Dreadnought - $25 - MSGDI - ✅ 채택
https://assetstore.unity.com/packages/3d/vehicles/space/ussn-freedom-class-dreadnought-195256
- Freedom class dreadnought ( 부분 모듈식이라 베리에이션 가능)
- US Space Navy Collection - $175 - MSGDI 의 구성을 따로 따로 파는 것중 하나
- 이것을 우선 사서 와이프와 이것저것 시도를 해보고 괜찮으면 US Space Navy Collection - $175 을 구매할 예정
- 기함의 최종 발전된 모습과 크기를 이것에 맞추고, 초기 모델은 이것의 일부를 떼서 크기 조정해서 쓸 예정

### US Space Navy Collection - $175 - MSGDI
https://assetstore.unity.com/packages/3d/vehicles/space/us-space-navy-collection-195258
- 크기 기준 함선 나열
- Freedom class dreadnought (scale 15, 15, 50 unity unit / 1000m) -> 1 unity unit = 20m 인격
- Arizona class battleship, Missouri class battleship, Yorktown class carrier
- Ticonderoga class battlecruiser, Charleston class cruiser
- Farragut class destroyer
- Congress class frigate (scale 2, 1, 4 unity unit / 80m)
- 이외 USSN Modular Fighter Kit (example 4종)
- 레이저, 미사일 포대 모듈 / 미사일 모델
- 종합적으로 봤을때 끝판왕급. 이정도 에셋 몇개 있으면 충분할 정도

### Spaceship Capital Ship Modular Equipment - $12.50 - MSGDI
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-capital-ship-modular-equipment-77852#content
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-dreadnought-modular-equipment-82645
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-cruiser-modular-equipment-75737
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-destroyer-modular-equipment-72924
- 종합 페키지에 이것이 없을 경우 추가 구매 가능

### Spaceship Capital Ship Collection I - $75 - MSGDI
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-capital-ship-collection-i-78939#content
- 함선 4개 및 파츠 모듈 미사일

### Spaceship Capital Ship Collection II - $67.50 - MSGDI
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-capital-ship-collection-ii-110044
- 함선 4개 및 파츠 모듈 미사일

### Spaceship Pirate Fleet Collection I - $50 - MSGDI
https://assetstore.unity.com/packages/3d/vehicles/space/spaceship-pirate-fleet-collection-i-75094
- 함선 4개 및 파츠 모듈 미사일
- 근데 이건 왜 싸지 오래되서?

### Ultimate Spaceships Creator — $95 - Ebal Studios
https://assetstore.unity.com/packages/3d/vehicles/space/ultimate-spaceships-creator-196802
- Cockpit / Body Parts / Wing 파츠가 별개 메시로 분리되어 있음 → 파츠 조합으로 신규 함선 디자인 가능
- 완성 함선 11종+ 제공 (Astro Eagle, Cosmic Shark, Force Badger 등)
- 파괴 연출: 파츠별 Rigidbody 적용으로 분리 날아가는 연출 가능
- URP / HDRP 지원

### Sci-Fi Modular Capital Spaceship Galactic Leopard - $40 - Ebal Studios
https://assetstore.unity.com/packages/3d/vehicles/space/sci-fi-modular-capital-spaceship-galactic-leopard-134390
- Ultimate Spaceships Creator 에서 함선 부분만 때어낸 것 같음

### Sci-Fi Space Stations Creator - $75 - Ebal Studios
https://assetstore.unity.com/packages/3d/vehicles/space/sci-fi-space-stations-creator-280237



## 이팩트 에셋
현재 검토 중인 에셋 목록. 최종 채택 전 단계.
상세한 모듈화 - 성능 문제 체크!
https://assetstore.unity.com/ko-KR/search#q=Sci-Fi&cf-ec_category=vfx,particles

### Sci-Fi Effects - $35 - FORGE3D - ✅ 채택
https://assetstore.unity.com/packages/vfx/particles/sci-fi-effects-20416
- Built-in / URP / HDRP 모두 호환, 2024년 4월 업데이트, 평점 5/5 (397개).
- 리뷰에서 "코드 느림 → 이펙트만 쓰고 무기 발사 시스템 코드는 버려라" 는데, 우리는 어차피 이펙트만 쓸 거니까 문제없어.






## 스카이박스 / 우주 배경 천체
### Space Skybox Kit — $10 (채택) - ✅ 채택
https://assetstore-fallback.unity.com/packages/2d/textures-materials/space-skybox-kit-176564?utm_source=chatgpt.com#content

- 큐브맵 16개 + 소행성 스프라이트 10개 + 행성 스프라이트 6개 + **PSD 원본 포함** (레이어 편집 가능)
- VFX Graph 미사용, 커스텀 셰이더 없음 → Android 빌드 안전, Draw Call 1회
- 2D 스프라이트 기반이라 3D 입체감 없음 → 배경 연출 전용 용도에 적합
- **Android 적용 시 필수 설정** (Texture Import Settings > Override for Android):
  - Max Size: `2048` / Format: `ASTC 6x6` (최신) 또는 `ETC2` (구형) / Generate Mipmaps: `OFF`
  - 원본 4096 큐브맵 비압축 ~192MB → 위 설정 시 ~10MB로 절감

### [후보 C] Space Graphics Toolkit (SGT) — $99.95 (세일 시 $49.97)
https://assetstore.unity.com/packages/tools/level-design/space-graphics-toolkit-4160

스카이박스뿐 아니라 행성/블랙홀/소행성/강착원반까지 통합 패키지. 평점 5.0 / 리뷰 390개. 9년 이상 업데이트 중.

**포함 컴포넌트 및 Android 가능 여부:**

| 컴포넌트 | 용도 | Android |
|---|---|---|
| SgtSkysphere | 우주 배경 (스카이박스 대체) | 가능 |
| SgtStarfield | 별 배경 수만 개 | 가능 (Billboard 기반) |
| SgtBelt | 소행성대 수천 개 | 가능 (GPU Instanced) |
| SgtRing | 강착원반 / 행성 고리 | 가능 (광산란 OFF 조건) |
| SgtCorona | 항성 코로나 | 가능 |
| SgtSphereShadow | 행성 그림자 | 가능 |
| SgtSingularity | 블랙홀 + 중력렌즈 왜곡 | **조건부** — URP에서 Opaque Texture + Depth Texture 강제 활성 필요, 대역폭 비용 |
| SgtAtmosphere | 볼류메트릭 대기권 | **주의** — Ray marching, 설정 최적화 필요 |
| SgtJovian | 볼류메트릭 가스행성 | **비권장** — Ray marching, 중하위 기기 프레임 드랍 |

**사용 전략 (이 프로젝트 기준):**
- 쓸 것: SgtSkysphere + SgtStarfield + SgtRing + SgtBelt + SgtSphereShadow
- 조건부: SgtSingularity — 블랙홀이 등장하는 특정 씬에서만 Opaque Texture 활성화
- 안 쓸 것: SgtJovian (가스행성은 정적 텍스처 구체로 대체)

**모바일 실사용 증거:** SGT 사용 게임 Super Starship 3, Space Trek 2150이 iOS/Android에 실제 출시됨 (개발자 Les Bird가 SGT 포럼에서 직접 증언)

**학습 난이도:** 기본 씬 구성은 Inspector 드래그앤드롭 수준. 공전/자전 등 게임 로직 연동 시 C# 작업 필요. 예제 씬 30개 이상 포함. 영상 튜토리얼 없음. 제작자가 포럼에서 직접 9년째 답변 중.

