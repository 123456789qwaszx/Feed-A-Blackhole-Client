# M6 자동 루프 (선택)

- 상태: 다음 (Q5 결정 필요)
- 브랜치: `feat/autoLoop` (← `feat/sheetSync`)
- 앞에서 받은 것:
  - AI가 읽을 곳: `PlaytestData/context/index.json`과 세팅별 묶음. 메모 저장 1초 안에 에디터가 쓴다(M4 `AiContextService`).
  - AI가 쓸 곳: `Assets/Playtest/Profiles/ai-draft.json` 하나. 쓰면 묶음의 draft 칸에 검사 결과가 다시 써진다.
  - AI 작업 지침 `Docs/BalanceLoop/AI-GUIDE.md`.
  - 승격·되돌리기(M4)와 시트 반영(M5)은 사람 버튼이다. 자동 루프도 이것을 바꾸지 않는다.

## 목표

메모를 저장하면 사람이 채팅으로 가지 않아도 AI가 돌아 초안 프로필을 만들고, 에디터에 "초안 도착"을 알린다. 승격은 언제나 사람이 한다.

## 선택지 (Q5)

| 안 | 어떻게 | 준비 | 장단점 |
| --- | --- | --- | --- |
| A | 지금처럼 채팅에서 "메모 읽고 반영해" | 없음 | 가장 단순. 사람이 한 번 말해야 한다 |
| B | 에디터가 로컬 Claude Code CLI를 헤드리스로 실행(`claude -p` + AI-GUIDE) | CLI 설치·로그인 | 가장 실시간. 실행마다 사용량이 든다 |
| C | Cowork 예약 작업이 일정 간격으로 새 메모를 확인 | 예약 작업, PC 연결 유지 | 사람 손이 안 든다. 지연이 간격만큼 생긴다 |

권장: M4까지 A로 돌려 본 뒤, 한 바퀴에 걸리는 시간이 문제가 되면 B를 검토한다.

## 설계 메모 (B를 고르면)

- 창의 "AI에게 보내기" 버튼과 "메모 저장 때 자동" 토글
- 실행: 백그라운드 프로세스로 돌린다. 결과(초안 경로·요약)를 `PlaytestData/ai-runs.ndjson`에 남긴다.
- 안전: 쓰기를 `Assets/Playtest/Profiles/ai-draft.json`과 `PlaytestData/`로만 제한한다. 승격과 시트 반영은 사람만 한다.
- 프롬프트 뼈대: "AI-GUIDE.md를 따라 PlaytestData/context/index.json에서 가장 최근 메모의 묶음을 읽고 ai-draft.json을 써라. 끝나면 세 줄 요약." 묶음이 다시 써질 때까지(draft.valid) 기다렸다가 결과를 기록한다.

## 결정 (시작할 때 확인)

- Q5 자동 실행을 할지, 한다면 B와 C 중 무엇으로 할지
