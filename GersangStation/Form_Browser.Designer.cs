namespace GersangStation {
    partial class Form_Browser {
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
            this.addressBar = new System.Windows.Forms.TextBox();
            this.goButton = new System.Windows.Forms.Button();
            this.button_saveShortcut = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // addressBar
            // 
            this.addressBar.Location = new System.Drawing.Point(9, 9);
            this.addressBar.Margin = new System.Windows.Forms.Padding(0);
            this.addressBar.Name = "addressBar";
            this.addressBar.Size = new System.Drawing.Size(872, 23);
            this.addressBar.TabIndex = 0;
            this.addressBar.KeyDown += new System.Windows.Forms.KeyEventHandler(this.addressBar_KeyDown);
            // 
            // goButton
            // 
            this.goButton.Location = new System.Drawing.Point(884, 9);
            this.goButton.Name = "goButton";
            this.goButton.Size = new System.Drawing.Size(75, 23);
            this.goButton.TabIndex = 1;
            this.goButton.Text = "Go!";
            this.goButton.UseVisualStyleBackColor = true;
            this.goButton.Click += new System.EventHandler(this.goButton_Click);
            // 
            // button_saveShortcut
            // 
            this.button_saveShortcut.Location = new System.Drawing.Point(965, 9);
            this.button_saveShortcut.Name = "button_saveShortcut";
            this.button_saveShortcut.Size = new System.Drawing.Size(127, 23);
            this.button_saveShortcut.TabIndex = 2;
            this.button_saveShortcut.Text = "나만의바로가기 저장";
            this.button_saveShortcut.UseVisualStyleBackColor = true;
            this.button_saveShortcut.Click += new System.EventHandler(this.button_saveShortcut_Click);
            // 
            // Form_Browser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1097, 761);
            this.Controls.Add(this.button_saveShortcut);
            this.Controls.Add(this.goButton);
            this.Controls.Add(this.addressBar);
            this.Name = "Form_Browser";
            this.Resize += new System.EventHandler(this.Form_Browser_Resize);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private TextBox addressBar;
        private Button goButton;
        private Button button_saveShortcut;
    }
}