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
            addressBar = new TextBox();
            goButton = new Button();
            button_saveShortcut = new Button();
            SuspendLayout();
            // 
            // addressBar
            // 
            addressBar.Location = new Point(9, 9);
            addressBar.Margin = new Padding(0);
            addressBar.Name = "addressBar";
            addressBar.Size = new Size(872, 23);
            addressBar.TabIndex = 0;
            addressBar.KeyDown += addressBar_KeyDown;
            // 
            // goButton
            // 
            goButton.Location = new Point(884, 9);
            goButton.Name = "goButton";
            goButton.Size = new Size(75, 23);
            goButton.TabIndex = 1;
            goButton.Text = "이동";
            goButton.UseVisualStyleBackColor = true;
            goButton.Click += goButton_Click;
            // 
            // button_saveShortcut
            // 
            button_saveShortcut.Location = new Point(965, 9);
            button_saveShortcut.Name = "button_saveShortcut";
            button_saveShortcut.Size = new Size(127, 23);
            button_saveShortcut.TabIndex = 2;
            button_saveShortcut.Text = "나만의바로가기 저장";
            button_saveShortcut.UseVisualStyleBackColor = true;
            button_saveShortcut.Click += button_saveShortcut_Click;
            // 
            // Form_Browser
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1097, 761);
            Controls.Add(button_saveShortcut);
            Controls.Add(goButton);
            Controls.Add(addressBar);
            Name = "Form_Browser";
            Resize += Form_Browser_Resize;
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private TextBox addressBar;
        private Button goButton;
        private Button button_saveShortcut;
    }
}