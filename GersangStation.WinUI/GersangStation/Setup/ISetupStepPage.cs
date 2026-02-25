using System;

namespace GersangStation.Setup
{
    internal interface ISetupStepPage
    {
        event EventHandler? StateChanged;
        bool CanGoNext { get; }
        bool CanSkip { get; }

        /// <summary>
        /// 다음 버튼 클릭 시 현재 페이지의 검증/상태 반영을 수행합니다.
        /// true를 반환하면 SetupWindow가 다음 스텝으로 전환합니다.
        /// </summary>
        bool OnNext();

        /// <summary>
        /// 건너뛰기 버튼 클릭 시 현재 페이지가 필요한 정리 작업을 수행합니다.
        /// </summary>
        void OnSkip();
    }
}
