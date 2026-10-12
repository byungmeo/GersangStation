# GameMaintenance.Worker

패치와 다중 클라이언트 생성을 작업할 때만 실행하는 x64 전용 프로세스입니다.
Windows Service를 설치하거나 등록하지 않습니다. 메인 앱은 `asInvoker`, 이 EXE는
`requireAdministrator`이며 앱이 `runas`로 실행합니다. 이미 승격된 앱에서 실행하면
추가 UAC 승인이 필요하지 않습니다.

## 구성과 권한 경계

- `Core`: 경로 검증, 버전 조회, 공용 결과 형식과 설치에 사용하는 공용 구성 요소.
- `GameMaintenance.Contracts`: 요청, 진행률, 취소, 오류 및 길이 제한된 named-pipe 프로토콜.
- `GameMaintenance.Worker`: 실제 패치와 다클라 파일 작업. 계정 저장소나 앱 설정 파일을 직접 읽지 않습니다.
- `GersangStation.Services.GameMaintenanceService`: 설정 스냅샷을 전달하고 작업을 직렬화합니다.

요청은 명령줄이나 임시 파일이 아닌 랜덤 이름의 named pipe로 전달합니다. 파이프 ACL과
양방향 PID 확인을 사용하며, 워커는 부모 프로세스의 실행 경로도 확인합니다.
앱 종료 또는 연결 해제 시 작업을 취소합니다. UAC 거절은 취소로 처리합니다.
취소는 파일/압축 처리 단위의 협력적 취소이며, 이미 적용한 패치를 롤백하지 않습니다.
실패/중단 시 임시 다운로드를 보존하는 기존 동작과 일반 폴더 충돌 검증을 유지합니다.

## 패키징과 검증

앱 프로젝트는 EXE와 자체 포함 .NET 런타임을 MSIX의 `Maintenance/`에 포함합니다.
두 앱 패키지 매니페스트의 `allowElevation`은 패키지 단위의 제한된 capability이며,
각 EXE의 실제 실행 권한 요구는 해당 EXE의 내장 실행 매니페스트로 구분합니다.
Microsoft Store 배포에는 이 capability에 대한 별도 승인이 필요합니다.

솔루션 전체를 Visual Studio MSBuild로 빌드합니다. 실제 배포 검증에는 설치된 MSIX에서
일반 사용자/UAC 승인·거절, 다른 관리자 자격 증명, 패치 취소, 자동 다클라 생성 및
앱 종료 시 워커 종료를 확인해야 합니다. 빌드 성공만으로 이 검증을 대체하지 않습니다.
