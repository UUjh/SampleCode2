# SampleCode2

간단한 Unity C# 샘플 코드입니다. 데스크톱 펫/상호작용, 데이터/오브젝트/사운드/창(UI), 간단한 애니메이션(Spine)과 이펙트, 등 가볍게 정리해 두었습니다. 포트폴리오 공개용이라 민감한 값은 비워두거나 예시 값으로 바꿔두었습니다.

## 폴더 구조
```
Managers/
  GameManager.cs         // 게임 루프/자동 코인/타이틀 페이드, 코인 스폰
  DataManager.cs         // 유저/설정/오브젝트 위치/구매/투두 관리, 저장/로드
  ObjectManager.cs       // 씬 내 오브젝트 등록/탐색, 배치 보조
  WindowManager.cs       // 캔버스 바닥 Y, 창 관련 유틸
  SoundManager.cs        // BGM/SFX 재생/볼륨
  PopupManager.cs        // 팝업 스택/모달 관리, 튜토리얼 표시
  LocalizationManager.cs // 간단한 다국어 키 관리
  TaskManager.cs         // 투두/알람 체크(스케줄)

Character/
  Controller/
    CharacterController.cs  // 상태 전이/클릭 보상/사운드/상호작용 입력 처리
  States/                   // Move/Climb/Decision 등 상태 클래스
  Data/
    CharacterData.cs        // 애니메이션/사운드 클립 등 데이터

Objects/
  Sofa.cs                   // 소파 상호작용(슬롯/정렬 등)

Core/
  Base/
    Singleton.cs, Utils.cs, IInteractable.cs // 공용 베이스/유틸

UI/
  BaseUI.cs                 // 공통 UI 베이스
  Panels/
    BasePanel.cs            // 패널 베이스
  ToolPanel/
    ToolPanel.cs            // 툴 패널(UI)
```

## 사용 기술
- Unity(C#), DOTween, Spine-Unity(스켈레톤 애니메이션), Windows API(Win32; 파일 드롭/창 제어), NAudio

## 주의사항
- 공개용 코드로, 일부 로직은 축약 또는 비활성화되어 있습니다.