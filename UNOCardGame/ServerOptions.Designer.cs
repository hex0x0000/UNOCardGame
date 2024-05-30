namespace UNOCardGame
{
    partial class ServerOptions
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
            maxPlayerLabel = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            ok = new System.Windows.Forms.Button();
            maxPlayers = new System.Windows.Forms.TextBox();
            motd = new System.Windows.Forms.TextBox();
            SuspendLayout();
            // 
            // maxPlayerLabel
            // 
            maxPlayerLabel.AutoSize = true;
            maxPlayerLabel.Location = new System.Drawing.Point(22, 21);
            maxPlayerLabel.Name = "maxPlayerLabel";
            maxPlayerLabel.Size = new System.Drawing.Size(153, 15);
            maxPlayerLabel.TabIndex = 0;
            maxPlayerLabel.Text = "Numero massimo di player:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(2, 55);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(173, 15);
            label2.TabIndex = 1;
            label2.Text = "Messaggio del server in entrata:";
            // 
            // ok
            // 
            ok.Location = new System.Drawing.Point(173, 112);
            ok.Name = "ok";
            ok.Size = new System.Drawing.Size(75, 23);
            ok.TabIndex = 2;
            ok.Text = "Ok";
            ok.UseVisualStyleBackColor = true;
            ok.Click += ok_Click;
            // 
            // maxPlayers
            // 
            maxPlayers.Location = new System.Drawing.Point(181, 18);
            maxPlayers.Name = "maxPlayers";
            maxPlayers.Size = new System.Drawing.Size(67, 23);
            maxPlayers.TabIndex = 3;
            maxPlayers.Text = "16";
            // 
            // motd
            // 
            motd.Location = new System.Drawing.Point(181, 52);
            motd.Multiline = true;
            motd.Name = "motd";
            motd.Size = new System.Drawing.Size(210, 47);
            motd.TabIndex = 4;
            motd.Text = "Benvenuto nel server!";
            // 
            // ServerOptions
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(418, 147);
            Controls.Add(motd);
            Controls.Add(maxPlayers);
            Controls.Add(ok);
            Controls.Add(label2);
            Controls.Add(maxPlayerLabel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Name = "ServerOptions";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "Opzioni del server";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label maxPlayerLabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button ok;
        private System.Windows.Forms.TextBox maxPlayers;
        private System.Windows.Forms.TextBox motd;
    }
}