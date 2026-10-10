# M3 플레이 메모

- 상태: 다음 (시작 전)
- 브랜치: `feat/feelNotes` (← `feat/liveData`)
- M2에서 받은 것: `PlaytestSession.Fingerprint`(이번 실행이 쓰는 값)·`BaseFingerprint`(프로필 전), 수치가 바뀌는 순간을 알리는 `LiveDataSignal`

## 목표

특정 세팅(노드·이정표·Level) 기준의 느낌을 에디터(테스트 세팅 창)나 게임(개발 패널)에서 적는다. 적는 순간 그때의 세팅·수치 지문·판 상태와 함께 JSON 한 줄로 바로 쌓이고, 사람과 AI가 같은 파일을 읽는다.

## 지금 상황

- 개발 패널 메모 탭이 `persistentDataPath/playtest/notes.ndjson`에 쓴다. 담기는 것은 판 ID·contentVersion·흐른 시간·Level·성장도·글뿐이다.
- 어떤 세팅인지(노드), 어떤 수치였는지(지문), 판이 어땠는지(속도)는 남지 않는다.
- 저장 위치가 PC마다 다른 앱 데이터 폴더라 AI가 찾기 어렵다.

## 범위

- 포함: 메모 형식 v1, 에디터 저장 위치(레포), 창의 메모 칸과 세팅별 메모 목록, 패널 메모 탭의 같은 형식 저장, 판 상태 스냅샷
- 제외: AI가 읽고 고치기(M4), 서버 전송, 팀 공유 동기화

## 설계

### 메모 한 줄 (schema 1)

```json
{
  "schema": 1,
  "id": "n-20261010-153012-7f3a",
  "atUtc": "2026-10-10T06:30:12Z",
  "setupKey": "c41d09e2",
  "setup": { "name": "stage1-lv15-golden", "growthStage": 1, "startLevel": 15, "seed": 7, "gold": 0,
             "nodes": [{ "nodeId": "timer-01", "rank": 1 }], "reachableInGame": false },
  "content": { "profile": "golden-x5", "fingerprint": "a1b2c3d4", "baseFingerprint": "9f8e7d6c" },
  "battle": { "battleId": "…", "elapsed": 23.4, "remaining": 4.2, "level": 16, "kills": 340, "gold": 12345,
              "rates5s": { "kills": 3.2, "gold": 1234, "exp": 456, "damage": 7890 },
              "alive": { "asteroid": 120, "planet": 2 }, "cheated": false },
  "difficulty": -2,
  "fun": 2,
  "tags": ["golden-too-strong"],
  "text": "황금이 너무 자주 터져서 Gold가 의미 없다",
  "intent": "성장도 0에서 황금 몫이 30% 정도면 좋겠다"
}
```

- `setupKey`: 이정표 단계·시작 Level·노드(id:rank 정렬)의 SHA-1 앞 8자리다. 이름이 달라도 같은 상태면 같은 키다.
- `battle`: 플레이 중에만 채운다. 에디터에서 세팅만 보고 적으면 null이다.
- `difficulty`: −2(너무 쉬움) ~ +2(너무 어려움), 안 고르면 null. `fun`: 1~5, 안 고르면 null.
- `tags`: 정해진 어휘에서 고른다(난이도·속도·재미·보상·버그 묶음). 어휘는 이 문서 끝에 둔다.
- `intent`: 어떻게 되면 좋겠는지 사람의 말로 적는다. AI가 가장 먼저 읽는다.

### 저장

- 에디터: `<레포>/PlaytestData/notes.ndjson`. `/PlaytestData/`는 `.gitignore`에 더한다(Q2).
- 개발 빌드: 지금처럼 `persistentDataPath/playtest/notes.ndjson`에 schema 1로 쓴다. 꺼내는 길은 adb pull이다.
- 저장 버튼을 누르면 한 줄을 붙이고 바로 디스크에 쓴다. 고치거나 지우지 않는다(덧붙이기만).
- 쓰기는 `PlaytestNotes`(런타임) 하나로 모은다. 에디터 창과 패널이 같은 함수를 부른다.

### 판 상태 스냅샷

- `PlaytestHud`가 글 대신 숫자 묶음 `HudSnapshot`을 만들고 글은 그 묶음에서 뽑는다.
- 메모는 그 묶음을 그대로 담는다.

### 화면

- 테스트 세팅 창의 "메모" 칸(창은 `LiveDataSignal`로 지문이 바뀌는 순간을 안다):
  - 난이도 5단계 버튼, 재미 1~5, 태그 칩, 글, 의도, 저장
  - 아래에 이 세팅 키의 최근 메모 목록(시간·지문·난이도·태그·글)
  - 지문이 지금과 다른 메모는 흐리게 보인다("이전 수치").
- 개발 패널 메모 탭: 같은 칸을 IMGUI로 줄여 둔다(난이도·태그·글·저장).

## 작업

- [ ] T1 메모 형식 v1과 `setupKey` + 헤드리스 테스트(같은 상태 = 같은 키, 순서 무관)
- [ ] T2 `HudSnapshot`과 HUD 글 분리
- [ ] T3 `PlaytestNotes` schema 1 쓰기, 에디터·기기 경로 나누기, `.gitignore`
- [ ] T4 창의 메모 칸과 세팅별 목록
- [ ] T5 패널 메모 탭 갱신
- [ ] T6 컴파일·헤드리스
- [ ] T7 문서 갱신(M3 결과, PLAN 점검, M4 문서 보정), 커밋

## 완료 기준

- [ ] 에디터에서 세팅만 보고 메모를 저장하면 `PlaytestData/notes.ndjson`에 battle이 null인 한 줄이 생긴다.
- [ ] 플레이 중 패널과 창 양쪽에서 저장하면 battle과 지문이 채워진다.
- [ ] 같은 노드·단계·Level을 다른 이름으로 저장해도 창의 목록에 함께 보인다.
- [ ] 수치를 바꾼 뒤(M2) 이전 메모가 "이전 수치"로 표시된다. 창을 닫았다 열어도 같다(메모의 지문과 지금 지문을 비교).
- [ ] 메모 파일을 바깥에서 고치거나 지워도 창이 깨지지 않는다(읽지 못한 줄은 건너뛰고 수를 알린다).
- [ ] 파일이 한 줄에 JSON 하나이고, AI가 줄 단위로 읽을 수 있다.

## 결정 (시작할 때 확인)

- Q2 메모 위치 → 권장: 레포 `PlaytestData/`(git 무시). 팀이 함께 보려면 나중에 커밋 대상으로 바꾼다.

## 태그 어휘 (초안)

| 묶음 | 태그 |
| --- | --- |
| 난이도 | `too-easy`, `easy`, `fair`, `hard`, `too-hard` (difficulty와 함께 쓰지 않아도 된다) |
| 속도 | `too-slow`, `too-fast`, `level-stall`, `level-rush` |
| 재미 | `boring`, `satisfying`, `chaotic` |
| 보상 | `gold-too-low`, `gold-too-high`, `golden-too-strong`, `node-pointless` |
| 기타 | `bug`, `visual`, `ui` |
