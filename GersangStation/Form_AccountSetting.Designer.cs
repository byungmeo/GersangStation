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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_AccountSetting));
            this.materialListBox1 = new MaterialSkin.Controls.MaterialListBox();
            this.materialButton_close = new GersangStation.CustomButton();
            this.label8 = new System.Windows.Forms.Label();
            this.materialButton_addAccount = new GersangStation.CustomButton();
            this.materialButton_removeAccount = new GersangStation.CustomButton();
            this.materialButton_changeAccount = new GersangStation.CustomButton();
            this.SuspendLayout();
            // 
            // materialListBox1
            // 
            this.materialListBox1.BackColor = System.Drawing.Color.White;
            this.materialListBox1.BorderColor = System.Drawing.Color.LightGray;
            this.materialListBox1.Depth = 0;
            this.materialListBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.materialListBox1.Location = new System.Drawing.Point(17, 101);
            this.materialListBox1.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialListBox1.Name = "materialListBox1";
            this.materialListBox1.SelectedIndex = -1;
            this.materialListBox1.SelectedItem = null;
            this.materialListBox1.Size = new System.Drawing.Size(248, 160);
            this.materialListBox1.TabIndex = 44;
            this.materialListBox1.TabStop = false;
            // 
            // materialButton_close
            // 
            this.materialButton_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.materialButton_close.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_close.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_close.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_close.BorderRadius = 5;
            this.materialButton_close.BorderSize = 0;
            this.materialButton_close.FlatAppearance.BorderSize = 0;
            this.materialButton_close.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_close.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_close.ForeColor = System.Drawing.Color.White;
            this.materialButton_close.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_close.Image")));
            this.materialButton_close.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.materialButton_close.Location = new System.Drawing.Point(128, 279);
            this.materialButton_close.Name = "materialButton_close";
            this.materialButton_close.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_close.Size = new System.Drawing.Size(79, 36);
            this.materialButton_close.TabIndex = 101;
            this.materialButton_close.Text = "닫기";
            this.materialButton_close.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_close.TextColor = System.Drawing.Color.White;
            this.materialButton_close.UseVisualStyleBackColor = false;
            this.materialButton_close.Click += new System.EventHandler(this.materialButton_close_Click);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Noto Sans", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label8.Location = new System.Drawing.Point(17, 78);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(73, 20);
            this.label8.TabIndex = 102;
            this.label8.Text = "계정 목록";
            // 
            // materialButton_addAccount
            // 
            this.materialButton_addAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.materialButton_addAccount.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_addAccount.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_addAccount.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_addAccount.BorderRadius = 5;
            this.materialButton_addAccount.BorderSize = 0;
            this.materialButton_addAccount.FlatAppearance.BorderSize = 0;
            this.materialButton_addAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_addAccount.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_addAccount.ForeColor = System.Drawing.Color.White;
            this.materialButton_addAccount.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_addAccount.Image")));
            this.materialButton_addAccount.Location = new System.Drawing.Point(278, 101);
            this.materialButton_addAccount.Name = "materialButton_addAccount";
            this.materialButton_addAccount.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_addAccount.Size = new System.Drawing.Size(42, 36);
            this.materialButton_addAccount.TabIndex = 103;
            this.materialButton_addAccount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_addAccount.TextColor = System.Drawing.Color.White;
            this.materialButton_addAccount.UseVisualStyleBackColor = false;
            this.materialButton_addAccount.Click += new System.EventHandler(this.materialButton_addAccount_Click);
            // 
            // materialButton_removeAccount
            // 
            this.materialButton_removeAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.materialButton_removeAccount.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_removeAccount.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_removeAccount.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_removeAccount.BorderRadius = 5;
            this.materialButton_removeAccount.BorderSize = 0;
            this.materialButton_removeAccount.FlatAppearance.BorderSize = 0;
            this.materialButton_removeAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_removeAccount.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_removeAccount.ForeColor = System.Drawing.Color.White;
            this.materialButton_removeAccount.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_removeAccount.Image")));
            this.materialButton_removeAccount.Location = new System.Drawing.Point(278, 143);
            this.materialButton_removeAccount.Name = "materialButton_removeAccount";
            this.materialButton_removeAccount.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_removeAccount.Size = new System.Drawing.Size(42, 36);
            this.materialButton_removeAccount.TabIndex = 104;
            this.materialButton_removeAccount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_removeAccount.TextColor = System.Drawing.Color.White;
            this.materialButton_removeAccount.UseVisualStyleBackColor = false;
            this.materialButton_removeAccount.Click += new System.EventHandler(this.materialButton_removeAccount_Click);
            // 
            // materialButton_changeAccount
            // 
            this.materialButton_changeAccount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.materialButton_changeAccount.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_changeAccount.BackgroundColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(71)))), ((int)(((byte)(79)))));
            this.materialButton_changeAccount.BorderColor = System.Drawing.Color.PaleVioletRed;
            this.materialButton_changeAccount.BorderRadius = 5;
            this.materialButton_changeAccount.BorderSize = 0;
            this.materialButton_changeAccount.FlatAppearance.BorderSize = 0;
            this.materialButton_changeAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.materialButton_changeAccount.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.materialButton_changeAccount.ForeColor = System.Drawing.Color.White;
            this.materialButton_changeAccount.Image = ((System.Drawing.Image)(resources.GetObject("materialButton_changeAccount.Image")));
            this.materialButton_changeAccount.Location = new System.Drawing.Point(278, 185);
            this.materialButton_changeAccount.Name = "materialButton_changeAccount";
            this.materialButton_changeAccount.Padding = new System.Windows.Forms.Padding(5);
            this.materialButton_changeAccount.Size = new System.Drawing.Size(42, 36);
            this.materialButton_changeAccount.TabIndex = 105;
            this.materialButton_changeAccount.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.materialButton_changeAccount.TextColor = System.Drawing.Color.White;
            this.materialButton_changeAccount.UseVisualStyleBackColor = false;
            this.materialButton_changeAccount.Click += new System.EventHandler(this.materialButton_addAccount_Click);
            // 
            // Form_AccountSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(334, 335);
            this.Controls.Add(this.materialButton_changeAccount);
            this.Controls.Add(this.materialButton_removeAccount);
            this.Controls.Add(this.materialButton_addAccount);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.materialButton_close);
            this.Controls.Add(this.materialListBox1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_AccountSetting";
            this.ShowInTaskbar = false;
            this.Sizable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "계정 설정";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form_AccountSetting_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private MaterialSkin.Controls.MaterialListBox materialListBox1;
        private CustomButton materialButton_close;
        private Label label8;
        private CustomButton materialButton_addAccount;
        private CustomButton materialButton_removeAccount;
        private CustomButton materialButton_changeAccount;
    }
}