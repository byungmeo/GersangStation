namespace GersangStation
{
    partial class Form_IntegrityCheck
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            materialButton_start = new MaterialSkin.Controls.MaterialButton();
            StatusLabel = new MaterialSkin.Controls.MaterialLabel();
            progressBar = new MaterialSkin.Controls.MaterialProgressBar();
            materialExpansionPanel1 = new MaterialSkin.Controls.MaterialExpansionPanel();
            checkedListBox1 = new CheckedListBox();
            materialButton_findPath = new MaterialSkin.Controls.MaterialButton();
            textBox_clientPath = new TextBox();
            materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            materialExpansionPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // materialButton_start
            // 
            materialButton_start.AutoSize = false;
            materialButton_start.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_start.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_start.Depth = 0;
            materialButton_start.HighEmphasis = true;
            materialButton_start.Icon = null;
            materialButton_start.Location = new Point(6, 112);
            materialButton_start.Margin = new Padding(2, 3, 2, 3);
            materialButton_start.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_start.Name = "materialButton_start";
            materialButton_start.NoAccentTextColor = Color.Empty;
            materialButton_start.Size = new Size(394, 32);
            materialButton_start.TabIndex = 4;
            materialButton_start.Text = "검사";
            materialButton_start.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_start.UseAccentColor = false;
            materialButton_start.UseVisualStyleBackColor = true;
            materialButton_start.Click += materialButton_start_Click;
            // 
            // StatusLabel
            // 
            StatusLabel.AutoSize = true;
            StatusLabel.Depth = 0;
            StatusLabel.Font = new Font("Noto Sans KR", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            StatusLabel.Location = new Point(76, 77);
            StatusLabel.Margin = new Padding(2, 0, 2, 0);
            StatusLabel.MouseState = MaterialSkin.MouseState.HOVER;
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Padding = new Padding(0, 12, 0, 0);
            StatusLabel.Size = new Size(1, 0);
            StatusLabel.TabIndex = 5;
            // 
            // progressBar
            // 
            progressBar.Depth = 0;
            progressBar.Location = new Point(6, 104);
            progressBar.Margin = new Padding(2);
            progressBar.MouseState = MaterialSkin.MouseState.HOVER;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(394, 5);
            progressBar.TabIndex = 6;
            // 
            // materialExpansionPanel1
            // 
            materialExpansionPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialExpansionPanel1.BackColor = Color.FromArgb(255, 255, 255);
            materialExpansionPanel1.Collapse = true;
            materialExpansionPanel1.Controls.Add(checkedListBox1);
            materialExpansionPanel1.Depth = 0;
            materialExpansionPanel1.Description = "";
            materialExpansionPanel1.DrawShadows = false;
            materialExpansionPanel1.ExpandHeight = 200;
            materialExpansionPanel1.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            materialExpansionPanel1.ForeColor = Color.FromArgb(222, 0, 0, 0);
            materialExpansionPanel1.Location = new Point(6, 152);
            materialExpansionPanel1.Margin = new Padding(8, 0, 8, 0);
            materialExpansionPanel1.MouseState = MaterialSkin.MouseState.HOVER;
            materialExpansionPanel1.Name = "materialExpansionPanel1";
            materialExpansionPanel1.Padding = new Padding(12, 70, 12, 8);
            materialExpansionPanel1.ShowValidationButtons = false;
            materialExpansionPanel1.Size = new Size(394, 48);
            materialExpansionPanel1.TabIndex = 8;
            materialExpansionPanel1.Tag = "";
            materialExpansionPanel1.Title = "결과 자세히 보기";
            // 
            // checkedListBox1
            // 
            checkedListBox1.BorderStyle = BorderStyle.None;
            checkedListBox1.Dock = DockStyle.Top;
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(12, 70);
            checkedListBox1.Margin = new Padding(2);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(370, 90);
            checkedListBox1.TabIndex = 4;
            checkedListBox1.ItemCheck += checkedListBox1_ItemCheck;
            // 
            // materialButton_findPath
            // 
            materialButton_findPath.AutoSize = false;
            materialButton_findPath.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_findPath.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_findPath.Depth = 0;
            materialButton_findPath.HighEmphasis = true;
            materialButton_findPath.Icon = null;
            materialButton_findPath.Location = new Point(356, 73);
            materialButton_findPath.Margin = new Padding(4, 6, 4, 6);
            materialButton_findPath.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_findPath.Name = "materialButton_findPath";
            materialButton_findPath.NoAccentTextColor = Color.Empty;
            materialButton_findPath.Size = new Size(33, 23);
            materialButton_findPath.TabIndex = 72;
            materialButton_findPath.Text = "...";
            materialButton_findPath.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_findPath.UseAccentColor = false;
            materialButton_findPath.UseVisualStyleBackColor = true;
            materialButton_findPath.Click += materialButton_findPath_Click;
            // 
            // textBox_clientPath
            // 
            textBox_clientPath.Location = new Point(96, 73);
            textBox_clientPath.Name = "textBox_clientPath";
            textBox_clientPath.PlaceholderText = "예시 : C:\\AKInteractive\\Gersang";
            textBox_clientPath.Size = new Size(250, 23);
            textBox_clientPath.TabIndex = 73;
            // 
            // materialLabel1
            // 
            materialLabel1.AutoSize = true;
            materialLabel1.Depth = 0;
            materialLabel1.Font = new Font("Noto Sans KR", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            materialLabel1.Location = new Point(19, 76);
            materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            materialLabel1.Name = "materialLabel1";
            materialLabel1.Size = new Size(70, 18);
            materialLabel1.TabIndex = 75;
            materialLabel1.Text = "본클라 경로";
            // 
            // Form_IntegrityCheck
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(409, 207);
            Controls.Add(materialLabel1);
            Controls.Add(materialButton_findPath);
            Controls.Add(textBox_clientPath);
            Controls.Add(materialExpansionPanel1);
            Controls.Add(progressBar);
            Controls.Add(StatusLabel);
            Controls.Add(materialButton_start);
            Margin = new Padding(2);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_IntegrityCheck";
            Padding = new Padding(2, 73, 2, 2);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "클라이언트 유효성 검사 - 현재 본서버만 가능";
            TopMost = true;
            Load += Form_IntegrityCheck_Load;
            materialExpansionPanel1.ResumeLayout(false);
            materialExpansionPanel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MaterialSkin.Controls.MaterialButton materialButton_start;
        private MaterialSkin.Controls.MaterialLabel StatusLabel;
        private MaterialSkin.Controls.MaterialProgressBar progressBar;
        private MaterialSkin.Controls.MaterialExpansionPanel materialExpansionPanel1;
        private CheckedListBox checkedListBox1;
        private MaterialSkin.Controls.MaterialButton materialButton_findPath;
        private TextBox textBox_clientPath;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
    }
}