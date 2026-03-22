namespace GersangStation {
    partial class Form_AccountSetting {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
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
            materialListBox1 = new MaterialSkin.Controls.MaterialListBox();
            linkLabel1 = new LinkLabel();
            materialLabel1 = new MaterialSkin.Controls.MaterialLabel();
            materialLabel2 = new MaterialSkin.Controls.MaterialLabel();
            materialButton_addAccount = new MaterialSkin.Controls.MaterialButton();
            materialButton_removeAccount = new MaterialSkin.Controls.MaterialButton();
            materialButton_changeAccount = new MaterialSkin.Controls.MaterialButton();
            SuspendLayout();
            // 
            // materialListBox1
            // 
            materialListBox1.BackColor = Color.White;
            materialListBox1.BorderColor = Color.LightGray;
            materialListBox1.Depth = 0;
            materialListBox1.Font = new Font("Microsoft Sans Serif", 16F, FontStyle.Regular, GraphicsUnit.Point);
            materialListBox1.Location = new Point(17, 101);
            materialListBox1.MouseState = MaterialSkin.MouseState.HOVER;
            materialListBox1.Name = "materialListBox1";
            materialListBox1.SelectedIndex = -1;
            materialListBox1.SelectedItem = null;
            materialListBox1.Size = new Size(248, 160);
            materialListBox1.TabIndex = 44;
            materialListBox1.TabStop = false;
            // 
            // linkLabel1
            // 
            linkLabel1.Location = new Point(59, 344);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(213, 23);
            linkLabel1.TabIndex = 107;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "[Notice] 계정정보와 해킹에 대한 안내";
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            // 
            // materialLabel1
            // 
            materialLabel1.AutoSize = true;
            materialLabel1.Depth = 0;
            materialLabel1.Font = new Font("Noto Sans KR Medium", 20F, FontStyle.Bold, GraphicsUnit.Pixel);
            materialLabel1.FontType = MaterialSkin.MaterialSkinManager.fontType.H6;
            materialLabel1.Location = new Point(17, 73);
            materialLabel1.MouseState = MaterialSkin.MouseState.HOVER;
            materialLabel1.Name = "materialLabel1";
            materialLabel1.Size = new Size(66, 25);
            materialLabel1.TabIndex = 108;
            materialLabel1.Text = "계정 목록";
            // 
            // materialLabel2
            // 
            materialLabel2.Depth = 0;
            materialLabel2.Font = new Font("Noto Sans KR", 14F, FontStyle.Regular, GraphicsUnit.Pixel);
            materialLabel2.HighEmphasis = true;
            materialLabel2.Location = new Point(53, 273);
            materialLabel2.MouseState = MaterialSkin.MouseState.HOVER;
            materialLabel2.Name = "materialLabel2";
            materialLabel2.Size = new Size(228, 61);
            materialLabel2.TabIndex = 109;
            materialLabel2.Text = "패스워드는 암호화되어 저장됩니다.\r\nPC방 등 공공장소에서는 사용 후 \r\n꼭 삭제해주세요. (필수)";
            materialLabel2.TextAlign = ContentAlignment.MiddleCenter;
            materialLabel2.UseAccent = true;
            // 
            // materialButton_addAccount
            // 
            materialButton_addAccount.AutoSize = false;
            materialButton_addAccount.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_addAccount.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_addAccount.Depth = 0;
            materialButton_addAccount.HighEmphasis = true;
            materialButton_addAccount.Icon = Properties.Resources.person_add;
            materialButton_addAccount.Location = new Point(281, 101);
            materialButton_addAccount.Margin = new Padding(4, 6, 4, 6);
            materialButton_addAccount.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_addAccount.Name = "materialButton_addAccount";
            materialButton_addAccount.NoAccentTextColor = Color.Empty;
            materialButton_addAccount.Size = new Size(36, 36);
            materialButton_addAccount.TabIndex = 110;
            materialButton_addAccount.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_addAccount.UseAccentColor = false;
            materialButton_addAccount.UseVisualStyleBackColor = true;
            materialButton_addAccount.Click += materialButton_addAccount_Click;
            // 
            // materialButton_removeAccount
            // 
            materialButton_removeAccount.AutoSize = false;
            materialButton_removeAccount.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_removeAccount.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_removeAccount.Depth = 0;
            materialButton_removeAccount.HighEmphasis = true;
            materialButton_removeAccount.Icon = Properties.Resources.person_cancel;
            materialButton_removeAccount.Location = new Point(281, 149);
            materialButton_removeAccount.Margin = new Padding(4, 6, 4, 6);
            materialButton_removeAccount.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_removeAccount.Name = "materialButton_removeAccount";
            materialButton_removeAccount.NoAccentTextColor = Color.Empty;
            materialButton_removeAccount.Size = new Size(36, 36);
            materialButton_removeAccount.TabIndex = 111;
            materialButton_removeAccount.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_removeAccount.UseAccentColor = false;
            materialButton_removeAccount.UseVisualStyleBackColor = true;
            materialButton_removeAccount.Click += materialButton_removeAccount_Click;
            // 
            // materialButton_changeAccount
            // 
            materialButton_changeAccount.AutoSize = false;
            materialButton_changeAccount.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            materialButton_changeAccount.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            materialButton_changeAccount.Depth = 0;
            materialButton_changeAccount.HighEmphasis = true;
            materialButton_changeAccount.Icon = Properties.Resources.edit;
            materialButton_changeAccount.Location = new Point(281, 197);
            materialButton_changeAccount.Margin = new Padding(4, 6, 4, 6);
            materialButton_changeAccount.MouseState = MaterialSkin.MouseState.HOVER;
            materialButton_changeAccount.Name = "materialButton_changeAccount";
            materialButton_changeAccount.NoAccentTextColor = Color.Empty;
            materialButton_changeAccount.Size = new Size(36, 36);
            materialButton_changeAccount.TabIndex = 112;
            materialButton_changeAccount.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            materialButton_changeAccount.UseAccentColor = false;
            materialButton_changeAccount.UseVisualStyleBackColor = true;
            materialButton_changeAccount.Click += materialButton_addAccount_Click;
            // 
            // Form_AccountSetting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(334, 379);
            Controls.Add(materialButton_changeAccount);
            Controls.Add(materialButton_removeAccount);
            Controls.Add(materialButton_addAccount);
            Controls.Add(materialLabel2);
            Controls.Add(materialLabel1);
            Controls.Add(linkLabel1);
            Controls.Add(materialListBox1);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_AccountSetting";
            ShowInTaskbar = false;
            Sizable = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "계정 추가 및 삭제";
            TopMost = true;
            Load += Form_AccountSetting_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private MaterialSkin.Controls.MaterialListBox materialListBox1;
        private LinkLabel linkLabel1;
        private MaterialSkin.Controls.MaterialLabel materialLabel1;
        private MaterialSkin.Controls.MaterialLabel materialLabel2;
        private MaterialSkin.Controls.MaterialButton materialButton_removeAccount;
        private MaterialSkin.Controls.MaterialButton materialButton_changeAccount;
        private MaterialSkin.Controls.MaterialButton materialButton_addAccount;
    }
}