# M4 AI 조정

- 상태: 다음 (시작 전)
- 브랜치: `feat/aiTuning` (← `feat/feelNotes`)
- M3에서 받은 것:
  - 메모 파일 `PlaytestData/notes.ndjson`(schema 1)과 세팅 키
  - `PlaytestJson`(쓰기·읽기)
  - 창의 `NoteFromWindow`·`CurrentSetupKey`, 패널의 `NewNote`, `HudSnapshot`
  - M2의 수치 지문과 `LiveDataSignal`

## 목표

AI가 메모·세팅·확정 정보·현재 수치를 읽고 의도에 맞게 수치를 바꾼다. 바꾼 값은 M2로 바로 반영되어 같은 세팅으로 다시 플레이한다. 모든 변경은 이유와 함께 기록되고 되돌릴 수 있다.

## 지금 상황

- 노드 수치는 CSV, 전투 기본값은 에셋(SO)에 있다. 팀 결정상 전투 기본값의 주인은 에셋이다.
- 밸런스 프로필 경로가 노드 비용·효과와 에셋 수치 대부분을 덮을 수 있다. 덮지 못하는 것: 수치 정의(기본값·Min·Max), 출현 띠 거리, 승격 대상.
- AI(Claude)는 사용자 PC의 레포 파일을 읽고 쓸 수 있다. Unity를 직접 돌리지는 못한다.

## 범위

- 포함: AI가 읽을 묶음(컨텍스트), 바꾸는 길(초안 → 승격), 변경 기록·되돌리기, AI 작업 지침 문서
- 제외: 기획 시트 반영(M5), 자동 실행(M6)

## 설계

### 1. AI가 읽을 묶음 `PlaytestData/context/<setupKey>.json`

- 에디터가 만든다. 시점: 메모 저장 때, 수치가 바뀔 때(M2) 지금 세팅에 대해, 창의 "AI 묶음 만들기" 버튼.
- 쓰는 법: `PlaytestJson`의 들여쓰기 쓰기로 사람도 읽기 좋게 쓴다. 메모는 화면용 `FeelNoteView`가 아니라 원문 JSON 객체를 넣는다. `PlaytestNotes`에 원문 읽기를 더한다.
- 담는 것:
  - 세팅과 `setupKey`
  - 확정 정보 전체(TestSetupReport 칸들)
  - 산 노드의 Rank별 비용·효과 행과 다음 Rank 행
  - 지금 프로필의 패치
  - 이 세팅 키의 메모(최근 20개)
  - 이 세팅에 걸린 변경 기록(최근 10개)
  - 수치 지문
- AI는 이 파일 하나만 읽으면 판단할 수 있어야 한다. Unity를 돌릴 수 없으므로 숫자는 모두 여기에 미리 계산해 둔다.

### 2. 바꾸는 길 (Q3 권장안: 초안 → 승격)

1. **초안**: AI가 `Assets/Playtest/Profiles/ai-draft.json`을 쓴다.
   - 형식은 지금 프로필과 같고, 패치마다 `reason`과 `noteIds`를 더한다(선택 필드).
   - M2가 바로 반영하고, 창은 "초안 있음 · 켜기"를 보인다.
2. **비교**: 같은 세팅·같은 시드로 원본과 초안을 번갈아 플레이한다(프로필 드롭다운).
3. **승격**: 창의 "초안을 원본에 반영" 명령이 다음을 한다.
   1. 노드 경로(`node/<id>/<rank>/cost`, `node/<id>/<rank>/<StatId>`)는 `NodeCost.csv`·`NodeEffects.csv`의 그 행을 고친다. 줄 순서, 따옴표, 천 단위 쉼표 같은 원래 서식은 그대로 둔다.
   2. 에셋 경로는 `SerializedObject`로 그 필드를 고치고 저장한다.
   3. 변경 기록에 남기고, 초안을 `Assets/Playtest/Profiles/archive/`로 옮긴다.
4. **되돌리기**: 변경 기록의 이전 값으로 같은 길을 거꾸로 간다.

에셋 경로 대응표:

| 프로필 경로 | 에셋 | 필드 |
| --- | --- | --- |
| `battle/timeLimit` · `battle/killTimeBonus` | BattleRules | `timeLimit` · `killTimeBonus` |
| `breaker/<field>` | SkillSetup | `breaker<Field>` (`planetBonusDamage`·`starBonusDamage`는 에셋 칸이 없어 승격하지 않는다. 노드 효과로만 바꾼다) |
| `enemy/<type>/<moveSpeed\|radius\|radiusStep\|spawnPeriod\|rainCount>` | Enemies/<Type> | 같은 이름 |
| `enemy/<type>/tier/<n>/<hp\|gold\|exp>` | Enemies/<Type> | `tiers[n-1].maxHealth\|gold\|exp` |
| `enemy/<type>/trait/<trait>/<field>` | Enemies/<Type> | `traits[i].<field>` (type이 trait인 i) |
| `supply/<type>/count` | EnemySupplySetup | `startSupply[i].count` (없으면 더함) |
| `growth/levelExp/<n>` | HqGrowthSetup | `levelExp[n-1]` |
| `growth/milestone/<n>/<field>` | HqGrowthSetup | `milestones[n-1].<field>` |

Q3 대안 "CSV·에셋을 바로 고친다"는 빠르지만, 원본과 나란히 비교할 수 없고 에셋 YAML(특히 16진 levelExp)을 글로 고치기 위험하다.

### 3. 변경 기록 `PlaytestData/changes.ndjson`

```json
{ "id": "c-20261010-161200-01", "atUtc": "…", "by": "ai", "kind": "promote",
  "patches": [{ "path": "enemy/asteroid/trait/golden/multiplier", "before": 50, "after": 10,
                "reason": "성장도 0 황금 몫 84% → 목표 30%, 예상 53%", "noteIds": ["n-…"] }],
  "fingerprintBefore": "9f8e7d6c", "fingerprintAfter": "1a2b3c4d",
  "sheetSynced": false }
```

- `sheetSynced`는 M5 전까지 늘 false다. 노드 경로 변경은 시트에 손으로 옮겨야 한다는 표시다.

### 4. AI 작업 지침 `Docs/BalanceLoop/AI-GUIDE.md`

- 읽는 순서: 메모의 `intent` → `difficulty`·`tags` → 확정 정보 → 노드 행 → 지난 변경
- 판단:
  1. 버그나 잘못 들어간 값부터 의심한다(더미 분석의 교훈).
  2. 가장 작은 손잡이 하나를 고른다.
  3. 바꾼 뒤의 숫자를 예상해 적는다(예: 황금 몫 = pm/((1−p)+pm)).
- 제한:
  - 한 번에 값 3개 이하, 한 값은 ×0.5~×2 범위(사용자가 넓히라고 하면 예외)
  - 노드 ID·Rank 수·선은 고치지 않는다
  - 이유와 메모 ID는 반드시 적는다
- 결과: 초안 프로필 + 채팅에 세 줄 요약(무엇을, 왜, 예상)
- 확인: Unity가 열려 있으면 다시 만들어진 묶음에서 새 확정 정보와 검증 오류 여부를 읽는다.

## 작업

- [ ] T1 컨텍스트 묶음 작성기와 형식(헤드리스로 실제 데이터 묶음 생성 확인)
- [ ] T2 프로필 패치의 `reason`·`noteIds` 필드, 창의 "초안 있음" 표시와 켜기
- [ ] T3 승격: CSV 행 편집기(서식 유지) + 에셋 대응표 편집기 + 변경 기록
- [ ] T4 되돌리기
- [ ] T5 `AI-GUIDE.md`
- [ ] T6 한 바퀴 시연: 실제 세팅 하나로 메모 → AI 초안 → 비교 → 승격 → 되돌리기(M1~M3 Unity 손 확인 뒤에 한다)
- [ ] T7 컴파일·헤드리스, 문서 갱신(M4 결과, PLAN 점검, M5 문서 보정), 커밋

## 완료 기준

- [ ] 메모를 저장하면 그 세팅의 컨텍스트 묶음이 생기고, 확정 정보·메모·노드 행이 들어 있다.
- [ ] AI가 쓴 초안이 몇 초 안에 반영되고, 원본과 같은 세팅·시드로 번갈아 플레이할 수 있다.
- [ ] 승격하면 CSV는 그 칸만, 에셋은 그 필드만 바뀐다(git diff로 확인). 변경 기록에 이전·이후 값이 남는다.
- [ ] 되돌리면 git diff가 비어 있는 상태로 돌아간다.
- [ ] 헤드리스: CSV 행 편집이 서식을 유지하고, 대응표의 모든 경로가 에셋 필드로 풀린다.

## 결정 (시작할 때 확인)

- Q3 AI 변경 방식 → 권장: 초안 프로필 → 승격
- 승격을 누가 누르는가 → 권장: 사람(창의 버튼). AI는 초안까지만
