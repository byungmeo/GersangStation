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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_AdvancedSetting));
            materialCheckbox_useBAT = new MaterialSkin.Controls.MaterialCheckbox();
            materialCheckbox_mouseClip = new MaterialSkin.Controls.MaterialCheckbox();
            toolTip1 = new ToolTip(components);
            img_help = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)img_help).BeginInit();
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
            // materialCheckbox_mouseClip
            // 
            materialCheckbox_mouseClip.AutoSize = true;
            materialCheckbox_mouseClip.Depth = 0;
            materialCheckbox_mouseClip.Location = new Point(37, 85);
            materialCheckbox_mouseClip.Margin = new Padding(0);
            materialCheckbox_mouseClip.MouseLocation = new Point(-1, -1);
            materialCheckbox_mouseClip.MouseState = MaterialSkin.MouseState.HOVER;
            materialCheckbox_mouseClip.Name = "materialCheckbox_mouseClip";
            materialCheckbox_mouseClip.ReadOnly = false;
            materialCheckbox_mouseClip.Ripple = true;
            materialCheckbox_mouseClip.Size = new Size(151, 37);
            materialCheckbox_mouseClip.TabIndex = 3;
            materialCheckbox_mouseClip.TabStop = false;
            materialCheckbox_mouseClip.Text = "향상된 마우스 가두기";
            materialCheckbox_mouseClip.UseVisualStyleBackColor = true;
            materialCheckbox_mouseClip.CheckedChanged += mouseClipCheckBox_CheckedChanged_1;
            // 
            // toolTip1
            // 
            toolTip1.AutoPopDelay = 10000;
            toolTip1.InitialDelay = 300;
            toolTip1.IsBalloon = true;
            toolTip1.ReshowDelay = 100;
            toolTip1.ToolTipIcon = ToolTipIcon.Info;
            toolTip1.ToolTipTitle = "향상된 마우스 가두기란?";
            // 
            // img_help
            // 
            img_help.Image = (Image)resources.GetObject("img_help.Image");
            img_help.Location = new Point(193, 94);
            img_help.Name = "img_help";
            img_help.Size = new Size(20, 20);
            img_help.SizeMode = PictureBoxSizeMode.StretchImage;
            img_help.TabIndex = 4;
            img_help.TabStop = false;
            // 
            // Form_AdvancedSetting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(399, 195);
            Controls.Add(img_help);
            Controls.Add(materialCheckbox_mouseClip);
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
            ((System.ComponentModel.ISupportInitialize)img_help).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_useBAT;
        private CustomButton materialButton_save;
        private MaterialSkin.Controls.MaterialCheckbox materialCheckbox_mouseClip;
        private ToolTip toolTip1;
        private PictureBox img_help;
    }
}