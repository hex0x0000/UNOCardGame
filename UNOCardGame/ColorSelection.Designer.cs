namespace UNOCardGame
{
    partial class ColorSelection
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
            redButton = new System.Windows.Forms.Button();
            blueButton = new System.Windows.Forms.Button();
            yellowButton = new System.Windows.Forms.Button();
            greenButton = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // redButton
            // 
            redButton.Location = new System.Drawing.Point(16, 9);
            redButton.Name = "redButton";
            redButton.Size = new System.Drawing.Size(150, 150);
            redButton.TabIndex = 1;
            redButton.Text = "Rosso";
            redButton.UseVisualStyleBackColor = true;
            redButton.Click += redButton_Click;
            // 
            // blueButton
            // 
            blueButton.Location = new System.Drawing.Point(172, 9);
            blueButton.Name = "blueButton";
            blueButton.Size = new System.Drawing.Size(150, 150);
            blueButton.TabIndex = 2;
            blueButton.Text = "Blu";
            blueButton.UseVisualStyleBackColor = true;
            blueButton.Click += blueButton_Click;
            // 
            // yellowButton
            // 
            yellowButton.Location = new System.Drawing.Point(16, 165);
            yellowButton.Name = "yellowButton";
            yellowButton.Size = new System.Drawing.Size(150, 150);
            yellowButton.TabIndex = 3;
            yellowButton.Text = "Giallo";
            yellowButton.UseVisualStyleBackColor = true;
            yellowButton.Click += yellowButton_Click;
            // 
            // greenButton
            // 
            greenButton.Location = new System.Drawing.Point(172, 165);
            greenButton.Name = "greenButton";
            greenButton.Size = new System.Drawing.Size(150, 150);
            greenButton.TabIndex = 4;
            greenButton.Text = "Verde";
            greenButton.UseVisualStyleBackColor = true;
            greenButton.Click += greenButton_Click;
            // 
            // ColorSelection
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(334, 317);
            Controls.Add(greenButton);
            Controls.Add(yellowButton);
            Controls.Add(blueButton);
            Controls.Add(redButton);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Name = "ColorSelection";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Seleziona un colore";
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button redButton;
        private System.Windows.Forms.Button blueButton;
        private System.Windows.Forms.Button yellowButton;
        private System.Windows.Forms.Button greenButton;
    }
}