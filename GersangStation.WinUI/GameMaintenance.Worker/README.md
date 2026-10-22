# GameMaintenance.Worker

설치·패치·다중 클라이언트 파일 작업에 사용하는 x64 전용 프로세스입니다.
메인 앱은 `asInvoker`, 워커는 `requireAdministrator`이며 앱이 `runas`로 실행합니다.
상주 Windows Service가 아니라 작업별로 생성되고 종료되는 프로세스입니다.

## 권한과 통신 경계

- `Core`: 경로/버전 검증, 설치 구성 요소와 공용 결과 형식.
- `GameMaintenance.Contracts`: 요청, 진행률, 취소, 오류 및 길이 제한된 named-pipe 프로토콜.
- `GameMaintenance.Worker`: 승격된 파일 작업. 계정 저장소와 앱 설정은 읽지 않습니다.
- `GameMaintenanceService`: 설정 스냅샷 전달, 워커 실행, 파일 작업 직렬화.

요청은 랜덤 이름의 named pipe로 전달합니다. 파이프 ACL과 양방향 PID 확인을 사용하며,
워커는 부모 프로세스 실행 경로를 확인합니다. 설치 대상은 승격 후 다시 검증합니다.
UAC 거절과 사용자 취소는 통신 단절·시간 초과·예상하지 못한 워커 종료와 구분됩니다.
작업 gate는 워커가 종료되어 더 이상 파일을 쓰지 않을 때 해제됩니다.

앱 종료나 연결 해제는 작업을 취소합니다. 취소는 협력적으로 수행되며 이미 적용된 파일을
롤백하지 않습니다. 설치 취소 시 선택한 다운로드 산출물 삭제도 같은 워커에서 처리합니다.
패치 적용 성공 후 임시 파일 정리 실패는 패치 성공을 뒤집지 않고 진단으로 전달합니다.

## 패키징

워커와 자체 포함 .NET 런타임은 MSIX의 `Maintenance/`에 포함됩니다.
`allowElevation`은 패키지 단위 capability이고 실제 EXE 권한은 각 실행 매니페스트로 결정됩니다.
Microsoft Store 배포에는 이 capability에 대한 별도 승인이 필요합니다.

파일 배치와 보존 규칙: [game policies](../docs/game-policies.md).
