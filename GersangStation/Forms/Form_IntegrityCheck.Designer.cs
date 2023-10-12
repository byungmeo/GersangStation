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
        private void InitializeComponent()
        {
            ClientPathTextBox = new MaterialSkin.Controls.MaterialTextBox2();
            materialButton1 = new MaterialSkin.Controls.MaterialButton();
            materialButton2 = new MaterialSkin.Controls.MaterialButton();
            StatusLabel = new MaterialSkin.Controls.MaterialLabel();
            progressBar = new MaterialSkin.Controls.MaterialProgressBar();
            materialExpansionPanel1 = new MaterialSkin.Controls.MaterialExpansionPanel();
            checkedListBox1 = new CheckedListBox();
            materialExpansionPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // ClientPathTextBox
            // 
            ClientPathTextBox.AnimateReadOnly = false;
            ClientPathTextBox.AutoCompleteMode = AutoCompleteMode.None;
            ClientPathTextBox.AutoCompleteSource = AutoCompleteSource.None;
            ClientPathTextBox.BackgroundImageLayout = ImageLayout.None;
            ClientPathTextBox.CharacterCasing = CharacterCasing.Normal;
            ClientPathTextBox.Depth = 0;
            ClientPathTextBox.Dock = DockStyle.Top;
            ClientPathTextBox.Enabled = false;
            ClientPathTextBox.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Regular, GraphicsUnit.Pixel);
            ClientPathTextBox.HideSelection = true;
            ClientPathTextBox.Hint = "예시 : C:\\AKInteractive\\Gersang";
            ClientPathTextBox.LeadingIcon = null;
            ClientPathTextBox.Location = new Point(5, 146);
            ClientPathTextBox.MaxLength = 32767;
            ClientPathTextBox.MouseState = MaterialSkin.MouseState.OUT;
            ClientPathTextBox.Name = "ClientPathTextBox";
            ClientPathTextBox.PasswordChar = '\0';
            ClientPathTextBox.PrefixSuffixText = null;
            ClientPathTextBox.ReadOnly = true;
            ClientPathTextBox.RightToLeft = RightToLeft.No;
            ClientPathTextBox.SelectedText = "";
            ClientPathTextBox.SelectionLength = 0;
            ClientPathTextBox.SelectionStart = 0;
            ClientPathTextBox.ShortcutsEnabled = false;
            ClientPathTextBox.Size = new Size(787, 48);
            ClientPathTextBox.TabIndex = 0;
            ClientPathTextBox.TabStop = false;
            ClientPathTextBox.TextAlign = HorizontalAlignment.Left;
            ClientPathTextBox.TrailingIcon = null;
            ClientPathTextBox.UseSystemPasswordChar = false;
            // 
            // materialButton1
            // 
            materialButton1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            materialButton1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton1.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton1.Depth = 0;
            materialButton1.HighEmphasis = true;
            materialButton1.Icon = null;
            materialButton1.Location = new Point(725, 152);
            materialButton1.Margin = new Padding(4, 6, 4, 6);
            materialButton1.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton1.Name = "materialButton1";
            materialButton1.NoAccentTextColor = Color.Empty;
            materialButton1.Size = new Size(64, 36);
            materialButton1.TabIndex = 1;
            materialButton1.Text = "...";
            materialButton1.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Outlined;
            materialButton1.UseAccentColor = false;
            materialButton1.UseVisualStyleBackColor = true;
            materialButton1.Click += materialButton1_Click;
            // 
            // materialButton2
            // 
            materialButton2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton2.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton2.Depth = 0;
            materialButton2.Dock = DockStyle.Top;
            materialButton2.HighEmphasis = true;
            materialButton2.Icon = null;
            materialButton2.Location = new Point(5, 194);
            materialButton2.Margin = new Padding(4, 6, 4, 6);
            materialButton2.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton2.Name = "materialButton2";
            materialButton2.NoAccentTextColor = Color.Empty;
            materialButton2.Size = new Size(787, 36);
            materialButton2.TabIndex = 4;
            materialButton2.Text = "실행";
            materialButton2.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton2.UseAccentColor = false;
            materialButton2.UseVisualStyleBackColor = true;
            materialButton2.Click += materialButton2_Click;
            // 
            // StatusLabel
            // 
            StatusLabel.AutoSize = true;
            StatusLabel.Depth = 0;
            StatusLabel.Font = new Font("Noto Sans KR", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            StatusLabel.Location = new Point(18, 216);
            StatusLabel.MouseState = MaterialSkin.MouseState.HOVER;
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Padding = new Padding(0, 23, 0, 0);
            StatusLabel.Size = new Size(1, 0);
            StatusLabel.TabIndex = 5;
            StatusLabel.Click += materialLabel1_Click;
            // 
            // progressBar
            // 
            progressBar.Depth = 0;
            progressBar.Dock = DockStyle.Top;
            progressBar.Location = new Point(5, 230);
            progressBar.MouseState = MaterialSkin.MouseState.HOVER;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(787, 5);
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
            materialExpansionPanel1.Dock = DockStyle.Top;
            materialExpansionPanel1.DrawShadows = false;
            materialExpansionPanel1.ExpandHeight = 200;
            materialExpansionPanel1.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            materialExpansionPanel1.ForeColor = Color.FromArgb(222, 0, 0, 0);
            materialExpansionPanel1.Location = new Point(5, 235);
            materialExpansionPanel1.Margin = new Padding(16, 1, 16, 0);
            materialExpansionPanel1.MouseState = MaterialSkin.MouseState.HOVER;
            materialExpansionPanel1.Name = "materialExpansionPanel1";
            materialExpansionPanel1.Padding = new Padding(24, 140, 24, 16);
            materialExpansionPanel1.ShowValidationButtons = false;
            materialExpansionPanel1.Size = new Size(787, 48);
            materialExpansionPanel1.TabIndex = 8;
            materialExpansionPanel1.Tag = "";
            materialExpansionPanel1.Title = "결과 자세히 보기";
            // 
            // checkedListBox1
            // 
            checkedListBox1.BorderStyle = BorderStyle.None;
            checkedListBox1.Dock = DockStyle.Top;
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(24, 140);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(739, 180);
            checkedListBox1.TabIndex = 4;
            checkedListBox1.ItemCheck += checkedListBox1_ItemCheck;
            // 
            // Form_IntegrityCheck
            // 
            AutoScaleDimensions = new SizeF(192F, 192F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(797, 238);
            Controls.Add(materialExpansionPanel1);
            Controls.Add(progressBar);
            Controls.Add(StatusLabel);
            Controls.Add(materialButton2);
            Controls.Add(materialButton1);
            Controls.Add(ClientPathTextBox);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_IntegrityCheck";
            Padding = new Padding(5, 146, 5, 5);
            ShowInTaskbar = false;
            Text = "클라이언트 유효성 검사 - 현재 본서버만 가능";
            TopMost = true;
            Load += Form_IntegrityCheck_Load;
            materialExpansionPanel1.ResumeLayout(false);
            materialExpansionPanel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MaterialSkin.Controls.MaterialTextBox2 ClientPathTextBox;
        private MaterialSkin.Controls.MaterialButton materialButton1;
        private MaterialSkin.Controls.MaterialButton materialButton2;
        private MaterialSkin.Controls.MaterialLabel StatusLabel;
        private MaterialSkin.Controls.MaterialProgressBar progressBar;
        private MaterialSkin.Controls.MaterialExpansionPanel materialExpansionPanel1;
        private CheckedListBox checkedListBox1;
    }
}