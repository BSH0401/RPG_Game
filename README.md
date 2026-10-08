# 달빛 우체국 (Moonlight Post Office)

밤마다 달라지는 섬을 탐험하며 편지를 배달하는 싱글 플레이 2D 탑다운 액션 RPG. Unity로 만든다.
기획서는 [Docs/GameDesign.md](Docs/GameDesign.md)에 있다.

현재 저장소에는 기획서의 **"첫 프로토타입 성공 기준"**(10~15분 분량)을 플레이할 수 있는 코드가 들어 있다.
그림과 소리는 [Ninja Adventure Asset Pack](https://pixel-boy.itch.io/ninja-adventure-asset-pack)(CC0, Pixel-boy & AAA)을 쓰고,
에셋이 없으면 코드로 그린 픽셀 아트(`Assets/Scripts/Art`)로 자동 대체된다. 씬을 직접 만들 필요 없이 Play를 누르면 월드가 코드로 생성된다.
**편집 화면(Scene/Hierarchy)이 비어 있는 것은 정상이다. Play를 눌러야 마을이 나타난다.**

![마을 미리보기](Docs/preview/village.png)

## 실행 방법

1. **Unity Hub → Add → Add project from disk**에서 이 폴더(`RPG_Game`)를 선택한다.
   - **Unity 6000.3.11f1** 기준이다. Unity Hub에서 이 버전을 설치해 두자.
   - 처음 열 때 *"새 Input System을 활성화할까요?"* 창이 뜨면 Yes/No 어느 쪽을 골라도 동작한다.
2. 열린 빈 씬(Untitled)에서 그대로 **Play** 버튼을 누른다.
   - `GameBootstrap`이 자동으로 생성되어 마을·숲·주민·적을 만든다.
3. 그림이 흐릿하거나 깨져 보이면 메뉴 **달빛 우체국 → 아트 다시 가져오기**를 누른다.
   (`Assets/Editor/PixelArtImporter.cs`가 픽셀 아트용 가져오기 설정을 자동으로 적용한다.)
4. 한글이 □로 보이면 한글 폰트 파일(.ttf)을 `Assets/Resources/Fonts/UIFont.ttf` 이름으로 넣는다.
   (예: 나눔고딕, Noto Sans KR. 보통 Windows·macOS에서는 없어도 보인다.)

## 플레이 흐름 (프로토타입)

| 순서 | 내용 |
|---|---|
| 1 | 우체국 창구(E)에서 편지 「밀가루가 묻은 편지」를 받는다. 받는 사람의 주소가 번져 있다. |
| 2 | 마을의 노아에게 물어보거나, 동쪽 숲에서 **터진 밀가루 자루**(단서)를 찾으면 받는 사람이 지도(Tab)에 표시된다. |
| 3 | 숲은 가운데 덤불 때문에 북쪽/남쪽 길로 나뉘며, **밤마다 한쪽이 쓰러진 나무로 막힌다**. 적과 단서 위치도 바뀐다. |
| 4 | 오두막의 오웬에게 편지를 전하며 **선택**(봉투째 건네기 / 소리 내 읽어주기)을 한다. 보상: 우편가방(최대 체력 +2). |
| 5 | 마을로 돌아오면 **빵집이 다시 문을 열고** 미라의 대사가 선택에 따라 달라진다. |
| 6 | 두 번째 편지(오웬의 답장): 마지막 줄을 전부 전할지, 빼고 전할지에 따라 미라가 서 있는 곳이 바뀐다. |
| 7 | 세 번째 편지(주소 없는 편지): 녹슨 표지판 단서 → 보스 「먹물 그림자」 → 낡은 우체통에 배달 → **우체국 등불이 켜진다**. |

## 조작

| 입력 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| 마우스 왼쪽 / J | 공격 (바라보는 방향) |
| Space / Shift | 회피 (짧은 무적) |
| E | 조사 · 대화 · 배달 / 대화 넘기기 |
| Q | 편지 도구 「봉인끈」: 주변 적을 잠시 묶음 (보스는 절반) |
| 1~3 / 클릭 | 선택지 고르기 |
| Tab / M | 지도와 의뢰 (게임 일시정지) |
| I | 가방 (가진 아이템 보기, 일시정지) |
| R | 크루아상 먹기 (체력 3 회복) |
| F1 | 조작법 보이기/숨기기 |
| F12 | 저장 삭제 후 새 게임 (개발용) |

전투 규칙: 적은 **빨간 예고 표시**(돌진 = 띠, 내려찍기 = 원)가 끝난 뒤에만 피해를 준다.
예고를 보고 회피하거나 봉인끈으로 끊는 것이 핵심이다. 쓰러지면 우체국 앞에서 다시 시작하고 편지는 유지된다.
우체국 창구에 말을 걸면 체력이 회복된다. 진행 상황은 편지를 받거나 배달할 때 자동 저장된다.

## 아이템

경험치 대신 **장비와 도구**로 강해진다. 장비는 가지고 있기만 하면 효과가 계속 적용된다.
아이템 정의는 `Assets/Resources/Data/items.json`에서 고친다.

| 아이템 | 종류 | 얻는 곳 | 효과 |
|---|---|---|---|
| 오웬의 우편가방 | 장비 | 첫 편지 배달 보상 | 최대 체력 +2 |
| 깃털 깔창 | 장비 | 첫 배달 뒤 노아에게 말 걸기 | 회피 시간 +40% |
| 은빛 봉인 인장 | 장비 | 숲 북서쪽 잠긴 상자 | 봉인끈 지속 +50%, 재사용 대기 단축 |
| 반딧불이 병 | 장비 | 오웬 오두막 뒤 상자 | 밤에 밝게 보이는 범위 확대 |
| 오두막 상자 열쇠 | 열쇠 | 첫 배달 뒤 오웬에게 말 걸기 | 잠긴 상자 열기 |
| 미라의 크루아상 | 소모품 | 빵집 진열대(밤마다 2개, 최대 5개) | R 키로 체력 3 회복 |

- 대사 조건에 `has:아이템id`(가지고 있음), `got:아이템id`(한 번이라도 얻음)를 쓸 수 있다.
- 주민이 아이템을 주게 하려면 `npcs.json`의 대사에 `"giveItem": "아이템id"`를 넣고,
  조건에 `!got:아이템id`를 넣어 한 번만 주게 한다.
- 편지 배달 보상은 `letters.json`의 `rewardItem`.

## 폴더 구조

```
Assets/
  Editor/                PixelArtImporter(픽셀 아트 가져오기 설정 자동 적용)
  Resources/Art/NinjaAdventure/  ← 사용하는 에셋만 골라 넣은 것(타일셋, 캐릭터, 얼굴, 효과, 음악·효과음, 라이선스)
  Resources/Data/        ← 스토리 데이터(JSON). 대사·편지·단서는 여기서 고친다.
    letters.json         편지(받는 조건, 단서 플래그, 배달 선택지, 결과 플래그, 보상)
    npcs.json            주민 대사(조건별, 위에서부터 처음 맞는 대사 사용)
    clues.json           단서
    items.json           아이템(장비·열쇠·소모품)
  Scripts/
    Core/                GameBootstrap(월드 생성), GameState(플래그·저장), GameInput, 공용 유틸
    Data/                JSON 데이터 정의와 로더
    Player/              PlayerController(이동·공격·회피·봉인끈·조사)
    Combat/              Health, EnemyController(예고 → 돌진/내려찍기, 기절, 보스 슈퍼아머)
    World/               LetterManager(핵심 루프), NightDirector(밤마다 바뀌는 숲),
                         WorldVisuals(플래그에 따른 마을 변화), Interactables, Spawner
    UI/                  HUD(체력·편지·지도·알림), DialogueSystem(대화·선택지)
    Art/                 GameAssets(에셋 자르기·불러오기), SpriteAnimator(4방향 걷기),
                         코드로 그린 대체 그림(Art, GroundPainter), 반딧불이
Docs/GameDesign.md       기획서
```

## 이야기를 확장하는 방법

모든 분기는 **문자열 플래그**로 처리된다. 조건 문법: `a,b` = a 그리고 b, `!a` = a가 아님,
`carrying:편지id` = 그 편지를 들고 있음, `delivered:편지id` = 배달 완료.

- **편지 추가**: `letters.json`에 항목을 추가한다. `condition`으로 언제 받을 수 있는지, `revealFlag`로 어떤 단서를 찾아야 받는 사람이 공개되는지, `choices[].setFlag`와 `deliveredFlag`로 배달 결과를 정한다.
- **대사 변화**: `npcs.json`의 `talks`에 조건과 대사를 추가한다. 더 구체적인 조건을 위쪽에 둔다.
- **마을 풍경 변화**: `GameBootstrap.BuildVillage()`에서 `WorldVisuals.Register(오브젝트, "조건")`으로 등록한다.
- **새 주민/단서 위치**: `Spawner.Npc(...)`, `GameBootstrap.Clue(...)`.
- **적 종류**: `Spawner.Enemy`에서 `EnemyController`의 수치와 `pattern`(Lunge/Slam 조합)을 바꿔 만든다.

## 다음 단계 (기획서 제작 순서 기준)

- [x] 1. 핵심 플레이 샘플 — 편지 수령 → 단서 → 배달 → 결과
- [x] 2. 전투 샘플 — 공격·회피·봉인끈, 적 공격 예고, 피격·리스폰
- [x] 3. 작은 완성 구역 — 마을 + 동쪽 숲(밤마다 변하는 길)
- [x] 4. 스토리 수직 단면 — 편지 3통, 주민 2명(+오웬), 보스 1종
- [ ] 이 프로토타입을 플레이해 보고 **"다음 편지가 궁금해지는가"** 확인
- [x] 임시 도형 → 코드로 그린 픽셀 아트, 밤 조명(가장자리 어둠·등불·반딧불이)
- [x] 에셋 적용: 타일 바닥(자동 타일), 건물·나무, 4방향 걷기 캐릭터, 대화 초상화, 효과음·배경음악
- [ ] 코드로 만드는 지형 → Unity 에디터에서 직접 편집하는 Tilemap 씬
- [x] 장비·도구 시스템: 아이템 6종, 가방 화면, 상자, 소모품
- [ ] 일반 적 2종 추가, 해안·등대 지역
- [ ] 편지 12통, 주민 6명, 엔딩 2종으로 확장

## 에셋 사용법과 출처

- 사용 에셋: **Ninja Adventure Asset Pack** — Pixel-boy & AAA, CC0 ([itch.io](https://pixel-boy.itch.io/ninja-adventure-asset-pack)).
  라이선스 전문은 `Assets/Resources/Art/NinjaAdventure/LICENSE.txt`.
- 게임에서 실제로 쓰는 파일만 `Assets/Resources/Art/NinjaAdventure/`에 넣었다(약 5MB). 원본 압축 파일은 저장소에 넣지 않는다.
- 어떤 그림을 쓰는지는 타일 좌표로 정해져 있다(`GameBootstrap.Assets.cs`, `GameAssets.cs`).
  예: `GameAssets.Tile("TilesetHouse", 25, 7, 4, 7)` = 집 타일셋의 (25, 7) 칸부터 가로 4칸·세로 7칸(우체국).
- 다른 캐릭터로 바꾸려면 원본 팩의 `Actor/Character/이름/SpriteSheet.png`를 `Actors/이름.png`로 복사하고,
  `GameBootstrap`의 `Spawner.Npc(..., "이름")`을 바꾼다.
