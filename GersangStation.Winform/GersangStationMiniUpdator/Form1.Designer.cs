namespace GersangStationMiniUpdator;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;
    private Label labelTitle;
    private Label labelTargetVersion;
    private Label labelTargetVersionValue;
    private Label labelTargetPath;
    private Label labelTargetPathValue;
    private Label labelStatus;
    private Label labelStatusValue;
    private ProgressBar progressBarMain;
    private TextBox textBoxLog;
    private Button buttonClose;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        labelTitle = new Label();
        labelTargetVersion = new Label();
        labelTargetVersionValue = new Label();
        labelTargetPath = new Label();
        labelTargetPathValue = new Label();
        labelStatus = new Label();
        labelStatusValue = new Label();
        progressBarMain = new ProgressBar();
        textBoxLog = new TextBox();
        buttonClose = new Button();
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(640, 360);
        Controls.Add(buttonClose);
        Controls.Add(textBoxLog);
        Controls.Add(progressBarMain);
        Controls.Add(labelStatusValue);
        Controls.Add(labelStatus);
        Controls.Add(labelTargetPathValue);
        Controls.Add(labelTargetPath);
        Controls.Add(labelTargetVersionValue);
        Controls.Add(labelTargetVersion);
        Controls.Add(labelTitle);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "거상 스테이션 미니 업데이트";

        labelTitle.AutoSize = true;
        labelTitle.Font = new Font("맑은 고딕", 14.25F, FontStyle.Bold, GraphicsUnit.Point);
        labelTitle.Location = new Point(18, 14);
        labelTitle.Name = "labelTitle";
        labelTitle.Size = new Size(220, 25);
        labelTitle.TabIndex = 0;
        labelTitle.Text = "거상 스테이션 미니 업데이트";

        labelTargetVersion.AutoSize = true;
        labelTargetVersion.Location = new Point(20, 55);
        labelTargetVersion.Name = "labelTargetVersion";
        labelTargetVersion.Size = new Size(71, 15);
        labelTargetVersion.TabIndex = 1;
        labelTargetVersion.Text = "대상 버전 :";

        labelTargetVersionValue.AutoEllipsis = true;
        labelTargetVersionValue.Location = new Point(97, 55);
        labelTargetVersionValue.Name = "labelTargetVersionValue";
        labelTargetVersionValue.Size = new Size(520, 15);
        labelTargetVersionValue.TabIndex = 2;
        labelTargetVersionValue.Text = "-";

        labelTargetPath.AutoSize = true;
        labelTargetPath.Location = new Point(20, 79);
        labelTargetPath.Name = "labelTargetPath";
        labelTargetPath.Size = new Size(71, 15);
        labelTargetPath.TabIndex = 3;
        labelTargetPath.Text = "대상 경로 :";

        labelTargetPathValue.AutoEllipsis = true;
        labelTargetPathValue.Location = new Point(97, 79);
        labelTargetPathValue.Name = "labelTargetPathValue";
        labelTargetPathValue.Size = new Size(520, 34);
        labelTargetPathValue.TabIndex = 4;
        labelTargetPathValue.Text = "-";

        labelStatus.AutoSize = true;
        labelStatus.Location = new Point(20, 120);
        labelStatus.Name = "labelStatus";
        labelStatus.Size = new Size(67, 15);
        labelStatus.TabIndex = 5;
        labelStatus.Text = "진행 상태 :";

        labelStatusValue.AutoEllipsis = true;
        labelStatusValue.Location = new Point(97, 120);
        labelStatusValue.Name = "labelStatusValue";
        labelStatusValue.Size = new Size(520, 15);
        labelStatusValue.TabIndex = 6;
        labelStatusValue.Text = "업데이트 준비 중...";

        progressBarMain.Location = new Point(20, 145);
        progressBarMain.Maximum = 100;
        progressBarMain.Name = "progressBarMain";
        progressBarMain.Size = new Size(597, 23);
        progressBarMain.TabIndex = 7;

        textBoxLog.Location = new Point(20, 183);
        textBoxLog.Multiline = true;
        textBoxLog.Name = "textBoxLog";
        textBoxLog.ReadOnly = true;
        textBoxLog.ScrollBars = ScrollBars.Vertical;
        textBoxLog.Size = new Size(597, 132);
        textBoxLog.TabIndex = 8;

        buttonClose.Enabled = false;
        buttonClose.Location = new Point(542, 323);
        buttonClose.Name = "buttonClose";
        buttonClose.Size = new Size(75, 27);
        buttonClose.TabIndex = 9;
        buttonClose.Text = "진행 중";
        buttonClose.UseVisualStyleBackColor = true;
        buttonClose.Click += buttonClose_Click;
    }

    #endregion
}
