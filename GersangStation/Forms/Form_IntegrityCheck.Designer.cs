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
            ClientPathTextBox.Enabled = false;
            ClientPathTextBox.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Regular, GraphicsUnit.Pixel);
            ClientPathTextBox.HelperText = "예시 : C:\\AKInteractive\\Gersang";
            ClientPathTextBox.HideSelection = true;
            ClientPathTextBox.LeadingIcon = null;
            ClientPathTextBox.Location = new Point(8, 193);
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
            ClientPathTextBox.Size = new Size(535, 48);
            ClientPathTextBox.TabIndex = 0;
            ClientPathTextBox.TabStop = false;
            ClientPathTextBox.TextAlign = HorizontalAlignment.Left;
            ClientPathTextBox.TrailingIcon = null;
            ClientPathTextBox.UseSystemPasswordChar = false;
            // 
            // materialButton1
            // 
            materialButton1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton1.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton1.Depth = 0;
            materialButton1.HighEmphasis = true;
            materialButton1.Icon = null;
            materialButton1.Location = new Point(550, 205);
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
            materialButton2.HighEmphasis = true;
            materialButton2.Icon = null;
            materialButton2.Location = new Point(622, 205);
            materialButton2.Margin = new Padding(4, 6, 4, 6);
            materialButton2.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton2.Name = "materialButton2";
            materialButton2.NoAccentTextColor = Color.Empty;
            materialButton2.Size = new Size(64, 36);
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
            StatusLabel.Location = new Point(18, 134);
            StatusLabel.MouseState = MaterialSkin.MouseState.HOVER;
            StatusLabel.Name = "StatusLabel";
            StatusLabel.Padding = new Padding(0, 23, 0, 0);
            StatusLabel.Size = new Size(1, 0);
            StatusLabel.TabIndex = 5;
            StatusLabel.Click += materialLabel1_Click;
            // 
            // Form_IntegrityCheck
            // 
            AutoScaleDimensions = new SizeF(192F, 192F);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new Size(797, 341);
            Controls.Add(StatusLabel);
            Controls.Add(materialButton2);
            Controls.Add(materialButton1);
            Controls.Add(ClientPathTextBox);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_IntegrityCheck";
            Padding = new Padding(5, 64, 5, 5);
            ShowInTaskbar = false;
            Text = "클라이언트 유효성 검사";
            TopMost = true;
            Load += Form_IntegrityCheck_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MaterialSkin.Controls.MaterialTextBox2 ClientPathTextBox;
        private MaterialSkin.Controls.MaterialButton materialButton1;
        private MaterialSkin.Controls.MaterialButton materialButton2;
        private MaterialSkin.Controls.MaterialLabel StatusLabel;
    }
}