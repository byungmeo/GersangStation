namespace GersangStation
{
    partial class Form_AdvancedSetting
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
            materialCheckbox_useBAT = new MaterialSkin.Controls.MaterialCheckbox();
            mouseClipCheckBox = new MaterialSkin.Controls.MaterialCheckbox();
            SuspendLayout();
            // 
            // materialCheckbox_useBAT
            // 
            materialCheckbox_useBAT.AutoSize = true;
            materialCheckbox_useBAT.Depth = 0;
            materialCheckbox_useBAT.Location = new Point(37, 125);
            materialCheckbox_useBAT.Margin = new Padding(0);
            materialCheckbox_useBAT.MouseLocation = new Point(-1, -1);
            materialCheckbox_useBAT.MouseState = MaterialSkin.MouseState.HOVER;
            materialCheckbox_useBAT.Name = "materialCheckbox_useBAT";
            materialCheckbox_useBAT.ReadOnly = false;
            materialCheckbox_useBAT.Ripple = true;
            materialCheckbox_useBAT.Size = new Size(326, 37);
            materialCheckbox_useBAT.TabIndex = 0;
            materialCheckbox_useBAT.TabStop = false;
            materialCheckbox_useBAT.Text = "다클라 생성 시 커맨드라인 방식 사용 (권장하지 않음)";
            materialCheckbox_useBAT.UseVisualStyleBackColor = true;
            materialCheckbox_useBAT.CheckedChanged += materialCheckbox_useBAT_CheckedChanged;
            // 
            // mouseClipCheckBox
            // 
            mouseClipCheckBox.AutoSize = true;
            mouseClipCheckBox.Depth = 0;
            mouseClipCheckBox.Location = new Point(37, 85);
            mouseClipCheckBox.Margin = new Padding(0);
            mouseClipCheckBox.MouseLocation = new Point(-1, -1);
            mouseClipCheckBox.MouseState = MaterialSkin.MouseState.HOVER;
            mouseClipCheckBox.Name = "mouseClipCheckBox";
            mouseClipCheckBox.ReadOnly = false;
            mouseClipCheckBox.Ripple = true;
            mouseClipCheckBox.Size = new Size(151, 37);
            mouseClipCheckBox.TabIndex = 3;
            mouseClipCheckBox.TabStop = false;
            mouseClipCheckBox.Text = "향상된 마우스 가두기";
            mouseClipCheckBox.UseVisualStyleBackColor = true;
            mouseClipCheckBox.CheckedChanged += mouseClipCheckBox_CheckedChanged_1;
            // 
            // Form_AdvancedSetting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(399, 195);
            Controls.Add(mouseClipCheckBox);
            Controls.Add(materialCheckbox_useBAT);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_AdvancedSetting";
            ShowInTaskbar = false;
            Sizable = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "고급 설정";
            TopMost = true;
            Load += Form_AdvancedSetting_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_useBAT;
        private CustomButton materialButton_save;
        private MaterialSkin.Controls.MaterialCheckbox mouseClipCheckBox;
    }
}