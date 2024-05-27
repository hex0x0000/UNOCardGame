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
            selectBg = new System.Windows.Forms.Button();
            selectName = new System.Windows.Forms.Button();
            previewLabel = new System.Windows.Forms.Label();
            labelPanel = new System.Windows.Forms.FlowLayoutPanel();
            SuspendLayout();
            // 
            // portLabel
            // 
            portLabel.AutoSize = true;
            portLabel.Location = new System.Drawing.Point(60, 167);
            portLabel.Name = "portLabel";
            portLabel.Size = new System.Drawing.Size(148, 15);
            portLabel.TabIndex = 0;
            portLabel.Text = "Inserisci la porta del server:";
            // 
            // nickLabel
            // 
            nickLabel.AutoSize = true;
            nickLabel.Location = new System.Drawing.Point(60, 30);
            nickLabel.Name = "nickLabel";
            nickLabel.Size = new System.Drawing.Size(137, 15);
            nickLabel.TabIndex = 1;
            nickLabel.Text = "Inserisci il tuo nickname:";
            // 
            // addressLabel
            // 
            addressLabel.AutoSize = true;
            addressLabel.Location = new System.Drawing.Point(60, 138);
            addressLabel.Name = "addressLabel";
            addressLabel.Size = new System.Drawing.Size(161, 15);
            addressLabel.TabIndex = 2;
            addressLabel.Text = "Inserisci l'indirizzo del server: ";
            // 
            // nickname
            // 
            nickname.Location = new System.Drawing.Point(227, 27);
            nickname.Name = "nickname";
            nickname.Size = new System.Drawing.Size(100, 23);
            nickname.TabIndex = 3;
            nickname.TextChanged += nickname_TextChanged;
            // 
            // address
            // 
            address.Location = new System.Drawing.Point(227, 135);
            address.Name = "address";
            address.Size = new System.Drawing.Size(100, 23);
            address.TabIndex = 4;
            // 
            // port
            // 
            port.Location = new System.Drawing.Point(227, 164);
            port.Name = "port";
            port.Size = new System.Drawing.Size(100, 23);
            port.TabIndex = 5;
            // 
            // isDNS
            // 
            isDNS.AutoSize = true;
            isDNS.Location = new System.Drawing.Point(333, 139);
            isDNS.Name = "isDNS";
            isDNS.Size = new System.Drawing.Size(83, 19);
            isDNS.TabIndex = 6;
            isDNS.Text = "E' un DNS?";
            isDNS.UseVisualStyleBackColor = true;
            // 
            // connect
            // 
            connect.Location = new System.Drawing.Point(105, 219);
            connect.Name = "connect";
            connect.Size = new System.Drawing.Size(92, 40);
            connect.TabIndex = 7;
            connect.Text = "Connettiti";
            connect.UseVisualStyleBackColor = true;
            connect.Click += connect_Click;
            // 
            // host
            // 
            host.Location = new System.Drawing.Point(301, 219);
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
            reconnect.Location = new System.Drawing.Point(203, 219);
            reconnect.Name = "reconnect";
            reconnect.Size = new System.Drawing.Size(92, 40);
            reconnect.TabIndex = 9;
            reconnect.Text = "Riconnettiti";
            reconnect.UseVisualStyleBackColor = true;
            reconnect.Click += reconnect_Click;
            // 
            // selectBg
            // 
            selectBg.Location = new System.Drawing.Point(60, 62);
            selectBg.Name = "selectBg";
            selectBg.Size = new System.Drawing.Size(143, 23);
            selectBg.TabIndex = 10;
            selectBg.Text = "Colore del background";
            selectBg.UseVisualStyleBackColor = true;
            selectBg.Click += selectBg_Click;
            // 
            // selectName
            // 
            selectName.Location = new System.Drawing.Point(60, 91);
            selectName.Name = "selectName";
            selectName.Size = new System.Drawing.Size(143, 23);
            selectName.TabIndex = 11;
            selectName.Text = "Colore dell'username";
            selectName.UseVisualStyleBackColor = true;
            selectName.Click += selectName_Click;
            // 
            // previewLabel
            // 
            previewLabel.AutoSize = true;
            previewLabel.Location = new System.Drawing.Point(209, 81);
            previewLabel.Name = "previewLabel";
            previewLabel.Size = new System.Drawing.Size(51, 15);
            previewLabel.TabIndex = 12;
            previewLabel.Text = "Preview:";
            // 
            // labelPanel
            // 
            labelPanel.Location = new System.Drawing.Point(266, 75);
            labelPanel.Name = "labelPanel";
            labelPanel.Size = new System.Drawing.Size(200, 32);
            labelPanel.TabIndex = 13;
            // 
            // ConnectForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(489, 275);
            Controls.Add(labelPanel);
            Controls.Add(previewLabel);
            Controls.Add(selectName);
            Controls.Add(selectBg);
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
        private System.Windows.Forms.Button selectBg;
        private System.Windows.Forms.Button selectName;
        private System.Windows.Forms.Label previewLabel;
        private System.Windows.Forms.FlowLayoutPanel labelPanel;
    }
}