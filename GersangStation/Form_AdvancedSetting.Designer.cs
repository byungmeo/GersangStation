namespace GersangStation {
    partial class Form_AdvancedSetting {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_AdvancedSetting));
            this.materialCheckbox_useBAT = new MaterialSkin.Controls.MaterialCheckbox();
            this.materialButton_save = new MaterialSkin.Controls.MaterialButton();
            this.SuspendLayout();
            // 
            // materialCheckbox_useBAT
            // 
            this.materialCheckbox_useBAT.AutoSize = true;
            this.materialCheckbox_useBAT.Depth = 0;
            this.materialCheckbox_useBAT.Location = new System.Drawing.Point(37, 85);
            this.materialCheckbox_useBAT.Margin = new System.Windows.Forms.Padding(0);
            this.materialCheckbox_useBAT.MouseLocation = new System.Drawing.Point(-1, -1);
            this.materialCheckbox_useBAT.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialCheckbox_useBAT.Name = "materialCheckbox_useBAT";
            this.materialCheckbox_useBAT.ReadOnly = false;
            this.materialCheckbox_useBAT.Ripple = true;
            this.materialCheckbox_useBAT.Size = new System.Drawing.Size(326, 37);
            this.materialCheckbox_useBAT.TabIndex = 0;
            this.materialCheckbox_useBAT.TabStop = false;
            this.materialCheckbox_useBAT.Text = "다클라 생성 시 커맨드라인 방식 사용 (권장하지 않음)";
            this.materialCheckbox_useBAT.UseVisualStyleBackColor = true;
            // 
            // materialButton_save
            // 
            this.materialButton_save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.materialButton_save.AutoSize = false;
            this.materialButton_save.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.materialButton_save.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.materialButton_save.Depth = 0;
            this.materialButton_save.HighEmphasis = true;
            this.materialButton_save.Icon = ((System.Drawing.Image)(resources.GetObject("materialButton_save.Icon")));
            this.materialButton_save.Location = new System.Drawing.Point(159, 143);
            this.materialButton_save.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.materialButton_save.MouseState = MaterialSkin.MouseState.HOVER;
            this.materialButton_save.Name = "materialButton_save";
            this.materialButton_save.NoAccentTextColor = System.Drawing.Color.Empty;
            this.materialButton_save.Size = new System.Drawing.Size(81, 36);
            this.materialButton_save.TabIndex = 39;
            this.materialButton_save.Text = "저장";
            this.materialButton_save.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.materialButton_save.UseAccentColor = false;
            this.materialButton_save.UseVisualStyleBackColor = true;
            this.materialButton_save.Click += new System.EventHandler(this.materialButton_save_Click);
            // 
            // Form_AdvancedSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(399, 195);
            this.Controls.Add(this.materialButton_save);
            this.Controls.Add(this.materialCheckbox_useBAT);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_AdvancedSetting";
            this.ShowInTaskbar = false;
            this.Sizable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "고급 설정";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.Form_AdvancedSetting_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_useBAT;
        private MaterialSkin.Controls.MaterialButton materialButton_save;
    }
}