namespace UNOCardGame
{
    partial class ConnectForm
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
            portLabel = new System.Windows.Forms.Label();
            nickLabel = new System.Windows.Forms.Label();
            addressLabel = new System.Windows.Forms.Label();
            nickname = new System.Windows.Forms.TextBox();
            address = new System.Windows.Forms.TextBox();
            port = new System.Windows.Forms.TextBox();
            isDNS = new System.Windows.Forms.CheckBox();
            connect = new System.Windows.Forms.Button();
            host = new System.Windows.Forms.Button();
            reconnect = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // portLabel
            // 
            portLabel.AutoSize = true;
            portLabel.Location = new System.Drawing.Point(60, 138);
            portLabel.Name = "portLabel";
            portLabel.Size = new System.Drawing.Size(148, 15);
            portLabel.TabIndex = 0;
            portLabel.Text = "Inserisci la porta del server:";
            // 
            // nickLabel
            // 
            nickLabel.AutoSize = true;
            nickLabel.Location = new System.Drawing.Point(60, 62);
            nickLabel.Name = "nickLabel";
            nickLabel.Size = new System.Drawing.Size(137, 15);
            nickLabel.TabIndex = 1;
            nickLabel.Text = "Inserisci il tuo nickname:";
            // 
            // addressLabel
            // 
            addressLabel.AutoSize = true;
            addressLabel.Location = new System.Drawing.Point(60, 102);
            addressLabel.Name = "addressLabel";
            addressLabel.Size = new System.Drawing.Size(161, 15);
            addressLabel.TabIndex = 2;
            addressLabel.Text = "Inserisci l'indirizzo del server: ";
            // 
            // nickname
            // 
            nickname.Location = new System.Drawing.Point(227, 54);
            nickname.Name = "nickname";
            nickname.Size = new System.Drawing.Size(100, 23);
            nickname.TabIndex = 3;
            // 
            // address
            // 
            address.Location = new System.Drawing.Point(227, 94);
            address.Name = "address";
            address.Size = new System.Drawing.Size(100, 23);
            address.TabIndex = 4;
            // 
            // port
            // 
            port.Location = new System.Drawing.Point(227, 130);
            port.Name = "port";
            port.Size = new System.Drawing.Size(100, 23);
            port.TabIndex = 5;
            // 
            // isDNS
            // 
            isDNS.AutoSize = true;
            isDNS.Location = new System.Drawing.Point(333, 101);
            isDNS.Name = "isDNS";
            isDNS.Size = new System.Drawing.Size(83, 19);
            isDNS.TabIndex = 6;
            isDNS.Text = "E' un DNS?";
            isDNS.UseVisualStyleBackColor = true;
            // 
            // connect
            // 
            connect.Location = new System.Drawing.Point(93, 202);
            connect.Name = "connect";
            connect.Size = new System.Drawing.Size(92, 40);
            connect.TabIndex = 7;
            connect.Text = "Connettiti";
            connect.UseVisualStyleBackColor = true;
            connect.Click += connect_Click;
            // 
            // host
            // 
            host.Location = new System.Drawing.Point(289, 202);
            host.Name = "host";
            host.Size = new System.Drawing.Size(92, 40);
            host.TabIndex = 8;
            host.Text = "Hosta la partita";
            host.UseVisualStyleBackColor = true;
            host.Click += host_Click;
            // 
            // reconnect
            // 
            reconnect.Enabled = false;
            reconnect.Location = new System.Drawing.Point(191, 202);
            reconnect.Name = "reconnect";
            reconnect.Size = new System.Drawing.Size(92, 40);
            reconnect.TabIndex = 9;
            reconnect.Text = "Riconnettiti";
            reconnect.UseVisualStyleBackColor = true;
            reconnect.Click += reconnect_Click;
            // 
            // ConnectForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(489, 297);
            Controls.Add(reconnect);
            Controls.Add(host);
            Controls.Add(connect);
            Controls.Add(isDNS);
            Controls.Add(port);
            Controls.Add(address);
            Controls.Add(nickname);
            Controls.Add(addressLabel);
            Controls.Add(nickLabel);
            Controls.Add(portLabel);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Name = "ConnectForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Connessione";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label portLabel;
        private System.Windows.Forms.Label nickLabel;
        private System.Windows.Forms.Label addressLabel;
        private System.Windows.Forms.TextBox nickname;
        private System.Windows.Forms.TextBox address;
        private System.Windows.Forms.TextBox port;
        private System.Windows.Forms.CheckBox isDNS;
        private System.Windows.Forms.Button connect;
        private System.Windows.Forms.Button host;
        private System.Windows.Forms.Button reconnect;
    }
}