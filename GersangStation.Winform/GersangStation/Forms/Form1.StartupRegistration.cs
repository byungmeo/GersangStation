using GersangStation.Modules;

namespace GersangStation;

public partial class Form1 {
    private async void materialButton_setting_startup_Click(object? sender, EventArgs e) {
        materialButton_setting_startup.Enabled = false;
        string executablePath = Application.ExecutablePath;
        try {
            string taskName = await Task.Run(() => StartupRegistrationService.Register(executablePath));
            if(IsDisposed) return;
            MessageBox.Show(this,
                "Windows 로그인 시 자동으로 실행되도록 등록했습니다.\n" +
                "자동 실행을 해제하려면 작업 스케줄러에서 아래 작업을 삭제해주세요.\n\n" + taskName,
                "시작 프로그램 등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch(Exception ex) {
            Logger.Log("시작 프로그램 등록 실패", ex);
            if(!IsDisposed) {
                MessageBox.Show(this, "시작 프로그램을 등록하지 못했습니다.\n" + ex.Message,
                    "시작 프로그램 등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        } finally {
            if(!IsDisposed) materialButton_setting_startup.Enabled = true;
        }
    }
}
