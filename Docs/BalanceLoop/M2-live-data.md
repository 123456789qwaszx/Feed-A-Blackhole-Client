# M2 수치 실시간 반영

- 상태: 다음 (시작 전)
- 브랜치: `feat/liveData` (← `feat/testSetup`)

## 목표

수치 파일(노드 CSV 4개, 전투 기본값 에셋 5종, 밸런스 프로필 JSON)이 디스크에서 바뀌면, 사람이 고쳤든 AI가 고쳤든 몇 초 안에 다음이 일어난다.

1. 테스트 세팅 창의 확정 정보가 새 값으로 바뀐다(창에 다시 들어가지 않아도).
2. 플레이 중이면 지금 세팅·같은 시드로 판이 다시 시작되어 새 값으로 플레이한다.
3. 어떤 수치로 플레이했는지를 짧은 지문(해시)으로 알 수 있다. 지문은 M3 메모와 M4 변경 기록의 열쇠가 된다.

## 지금 상황

- 게임은 `GameBootstrap.Awake`에서 수치를 한 번 읽는다. 판 구성은 판을 시작할 때 굳는다. 그래서 값이 바뀌면 판을 새로 만들어야 하고, 확실한 길은 장면을 다시 부르는 것이다.
- 노드 CSV는 TextAsset이라 Unity가 가져와야(import) 내용이 바뀐다. Unity의 Auto Refresh 설정이 "플레이 밖에서만"이면 플레이 중 바뀐 파일은 가져오지 않는다. 창이 뒤에 있을 때도 Unity가 앞에 올 때까지 가져오지 않는다.
- 에셋(SO)은 Inspector에서 고치면 메모리 값이 바로 바뀐다. 디스크 YAML이 바뀌면 가져올 때 다시 읽힌다.
- 테스트 세팅 창은 지금 OnFocus 때만 다시 읽는다.

## 범위

- 포함: 에디터(플레이 모드 포함)에서의 감시·반영, 수치 지문, 창·HUD 표시, 자동 다시 시작 켜고 끄기
- 제외: 개발 빌드(폰)의 실시간 반영(지금처럼 프로필 JSON을 adb push하고 패널에서 다시 읽기), 기획 시트(M5)

## 설계

### 1. 수치 지문 `ContentFingerprint` (개발 전용)

- 패치까지 적용한 `ContentData`와 `NodeContentData`를 리플렉션으로 필드 순서대로 풀어 쓴 글의 SHA-1 앞 8자리다.
- 에셋 YAML이나 `JsonUtility`를 쓰지 않는다. 객체 참조(instanceID)가 실행마다 달라져 같은 값에서도 지문이 바뀌기 때문이다.
- `PlaytestSession.Patch`가 패치 뒤에 계산해 `Fingerprint`로 둔다. 원본 지문과 적용 지문을 둘 다 남긴다.
- 표시: 창 제목 줄, HUD 첫 줄(`golden-x5 · a1b2c3d4`).
- 메모(M3), 변경 기록(M4), 컨텍스트 묶음(M4)에 넣는다.

### 2. 감시 `LiveDataWatcher` (에디터, `[InitializeOnLoad]`)

- 감시 대상:
  - `Assets/Data` 아래 `*.csv`, `*.asset`
  - `Assets/Playtest/Profiles/*.json`
  - `Assets/Playtest/Scenarios/*.json`(창의 파일 목록용)
- 길:
  1. 디스크 바뀜은 `FileSystemWatcher`가 잡는다. 이벤트는 스레드 안전한 큐에 넣는다.
  2. Unity 안 저장(Inspector에서 고친 뒤 Ctrl+S)은 `AssetPostprocessor.OnPostprocessAllAssets`가 잡는다.
  3. `EditorApplication.update`가 큐를 비운다. 마지막 바뀜 뒤 0.3초 조용하면 한 번에 처리한다.
  4. 플레이 중이면 바뀐 경로를 `AssetDatabase.ImportAsset(path, ForceUpdate)`로 직접 가져온다. 스크립트는 감시하지 않으므로 컴파일은 일어나지 않는다.
  5. `LiveData.Changed(바뀐 경로 목록)`를 낸다.
- git checkout처럼 한꺼번에 많이 바뀌면, 마지막 바뀜 뒤 한 번만 처리한다.

### 3. 창의 반응 (`TestSetupWindow`)

- `LiveData.Changed`를 받으면 콘텐츠를 다시 불러오고 확정 정보를 다시 계산한다.
- 알림을 띄운다: `수치 바뀜: NodeCost.csv · 지문 a1b2c3d4 → 9f8e7d6c`.
- 새 값이 검증을 통과하지 못하면 오류를 보이고, 확정 정보는 마지막으로 통과한 값을 유지한다.

### 4. 플레이 중 반응 (`PlaytestPanel`, Q1)

1. 에디터 쪽이 새 값을 같은 로더로 검증한다(`GameContentLoader.Load(setup, profile.Patch)`).
2. 통과하고 "수치가 바뀌면 다시 시작"(기본 켬, 판 탭 토글)이 켜져 있으면, 다시 시작할 세팅을 고른다.
   - 마지막 시나리오·세팅이 있으면 그것을 쓰고, 시드는 방금 판의 시드(`LastSeed`)를 쓴다.
   - 없으면 지금 진행(성장도·노드·Gold)과 판 Level을 세팅으로 떠서 쓴다.
3. `TestSetupLaunch`에 맡기고 장면을 다시 부른다. 프로필 바꾸기와 같은 길이다.
4. 통과하지 못하면 다시 시작하지 않는다. HUD와 패널에 `수치 오류 N개 — 이전 값으로 계속`을 보인다.
5. 런타임 쪽 신호는 `LiveDataSignal`(Assembly-CSharp, `#if UNITY_EDITOR`의 정적 이벤트)로 받는다. 에디터 감시가 이 신호를 낸다.

Q1 대안 "다음 판부터 적용"은 BattleSystem·ScreenFlow·노드 화면이 쥔 콘텐츠를 모두 바꿔 끼워야 한다. 고칠 곳이 많고 빠뜨리기 쉬워 하지 않는다.

## 만들 파일

| 파일 | 어셈블리 | 내용 |
| --- | --- | --- |
| `Playtest/ContentFingerprint.cs` | Assembly-CSharp(개발) | 리플렉션 풀어쓰기 + SHA-1 |
| `Playtest/LiveDataSignal.cs` | Assembly-CSharp(에디터) | 정적 이벤트, 검증 결과 전달 |
| `Editor/Playtest/LiveDataWatcher.cs` | Editor | 감시·디바운스·가져오기·검증·신호 |
| `PlaytestSession`·`PlaytestPanel`·`PlaytestHud`·`TestSetupWindow` 수정 | — | 지문 보관, 자동 다시 시작, 표시 |

## 작업

- [ ] T1 수치 지문 + 헤드리스 테스트(같은 값이면 같고, 값 하나 바뀌면 바뀌고, 패치 순서와 무관)
- [ ] T2 LiveDataWatcher(감시·디바운스·플레이 중 가져오기·검증)
- [ ] T3 LiveDataSignal과 패널의 자동 다시 시작(토글, 세팅 고르기, 실패 표시)
- [ ] T4 창의 즉시 반영과 알림, 지문 표시
- [ ] T5 HUD 지문
- [ ] T6 개발·릴리스·에디터 컴파일, 헤드리스
- [ ] T7 문서 갱신(M2 결과, PLAN 점검, M3 문서 보정), 커밋

## 완료 기준

- [ ] 플레이 밖: 메모장으로 `NodeCost.csv` 한 칸을 고쳐 저장하면 3초 안에 창의 확정 정보와 지문이 바뀐다.
- [ ] 플레이 중: 같은 수정이면 같은 세팅·같은 시드로 판이 다시 시작하고 HUD 지문이 바뀐다.
- [ ] 프로필 JSON 수정, Inspector에서 에셋 수정 후 저장도 같다.
- [ ] 잘못된 값(음수 제한 시간)은 다시 시작하지 않고 오류를 보인다. 고치면 다시 정상으로 돌아온다.
- [ ] git checkout으로 파일 수십 개가 바뀌어도 한 번만 처리한다.
- [ ] 헤드리스: 지문 테스트 통과. 기존 118개 유지.

## 결정 (시작할 때 확인)

- Q1 플레이 중 수치가 바뀌면 → 권장: 같은 세팅·같은 시드로 장면 다시 시작(토글로 끌 수 있음)
- Q6 contentVersion에 지문을 넣을지 → M2 끝에 서버 계약(64자) 안에서 `프로필@지문` 형식을 검토해 PLAN에 올린다

## 위험

- `FileSystemWatcher`가 일부 편집기의 임시 파일 저장(쓰기→이름 바꾸기)에 이벤트를 여러 번 낸다 → 확장자 필터와 디바운스
- 플레이 중 `ImportAsset`은 Unity 버전에 따라 경고를 낼 수 있다 → 손 확인 항목에 넣는다
- 장면 다시 부르기는 1~2초 걸린다 → 허용(목표는 몇 초 안)
