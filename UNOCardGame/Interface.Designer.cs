
namespace UNOCardGame
{
    partial class Interface
    {
        /// <summary>
        /// Variabile di progettazione necessaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Pulire le risorse in uso.
        /// </summary>
        /// <param name="disposing">ha valore true se le risorse gestite devono essere eliminate, false in caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Codice generato da Progettazione Windows Form

        /// <summary>
        /// Metodo necessario per il supporto della finestra di progettazione. Non modificare
        /// il contenuto del metodo con l'editor di codice.
        /// </summary>
        private void InitializeComponent()
        {
            this.drawButton = new System.Windows.Forms.Button();
            this.testButton = new System.Windows.Forms.Button();
            this.cards = new System.Windows.Forms.FlowLayoutPanel();
            this.colorLabel = new System.Windows.Forms.Label();
            this.players = new System.Windows.Forms.FlowLayoutPanel();
            this.playersLabel = new System.Windows.Forms.Label();
            this.turnDirection = new System.Windows.Forms.Label();
            this.cardsLabel = new System.Windows.Forms.Label();
            this.bluffButton = new System.Windows.Forms.Button();
            this.chat = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // drawButton
            // 
            this.drawButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 20.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.drawButton.Location = new System.Drawing.Point(378, 213);
            this.drawButton.Name = "drawButton";
            this.drawButton.Size = new System.Drawing.Size(110, 57);
            this.drawButton.TabIndex = 2;
            this.drawButton.Text = "Pesca";
            this.drawButton.UseVisualStyleBackColor = true;
            // 
            // testButton
            // 
            this.testButton.Location = new System.Drawing.Point(494, 79);
            this.testButton.Name = "testButton";
            this.testButton.Size = new System.Drawing.Size(193, 274);
            this.testButton.TabIndex = 4;
            this.testButton.Text = "Carta sul Tavolo";
            this.testButton.UseVisualStyleBackColor = true;
            // 
            // cards
            // 
            this.cards.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cards.Location = new System.Drawing.Point(12, 412);
            this.cards.Name = "cards";
            this.cards.Size = new System.Drawing.Size(1183, 309);
            this.cards.TabIndex = 5;
            // 
            // colorLabel
            // 
            this.colorLabel.AutoSize = true;
            this.colorLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.colorLabel.Location = new System.Drawing.Point(400, 34);
            this.colorLabel.Name = "colorLabel";
            this.colorLabel.Size = new System.Drawing.Size(88, 26);
            this.colorLabel.TabIndex = 6;
            this.colorLabel.Text = "Colore: ";
            // 
            // players
            // 
            this.players.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.players.Location = new System.Drawing.Point(12, 34);
            this.players.Name = "players";
            this.players.Size = new System.Drawing.Size(360, 359);
            this.players.TabIndex = 7;
            // 
            // playersLabel
            // 
            this.playersLabel.AutoSize = true;
            this.playersLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.playersLabel.Location = new System.Drawing.Point(8, 15);
            this.playersLabel.Name = "playersLabel";
            this.playersLabel.Size = new System.Drawing.Size(84, 24);
            this.playersLabel.TabIndex = 8;
            this.playersLabel.Text = "Giocatori";
            // 
            // turnDirection
            // 
            this.turnDirection.AutoSize = true;
            this.turnDirection.Font = new System.Drawing.Font("Microsoft Sans Serif", 30.25F);
            this.turnDirection.Location = new System.Drawing.Point(323, -8);
            this.turnDirection.Name = "turnDirection";
            this.turnDirection.Size = new System.Drawing.Size(49, 47);
            this.turnDirection.TabIndex = 9;
            this.turnDirection.Text = "↞";
            // 
            // cardsLabel
            // 
            this.cardsLabel.AutoSize = true;
            this.cardsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cardsLabel.Location = new System.Drawing.Point(12, 386);
            this.cardsLabel.Name = "cardsLabel";
            this.cardsLabel.Size = new System.Drawing.Size(71, 29);
            this.cardsLabel.TabIndex = 10;
            this.cardsLabel.Text = "Carte";
            // 
            // bluffButton
            // 
            this.bluffButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bluffButton.Location = new System.Drawing.Point(378, 150);
            this.bluffButton.Name = "bluffButton";
            this.bluffButton.Size = new System.Drawing.Size(110, 57);
            this.bluffButton.TabIndex = 11;
            this.bluffButton.Text = "Bluff!";
            this.bluffButton.UseVisualStyleBackColor = true;
            // 
            // chat
            // 
            this.chat.FormattingEnabled = true;
            this.chat.Location = new System.Drawing.Point(693, 12);
            this.chat.Name = "chat";
            this.chat.Size = new System.Drawing.Size(502, 381);
            this.chat.TabIndex = 12;
            // 
            // Interface
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1207, 733);
            this.Controls.Add(this.chat);
            this.Controls.Add(this.bluffButton);
            this.Controls.Add(this.cardsLabel);
            this.Controls.Add(this.turnDirection);
            this.Controls.Add(this.playersLabel);
            this.Controls.Add(this.players);
            this.Controls.Add(this.colorLabel);
            this.Controls.Add(this.cards);
            this.Controls.Add(this.testButton);
            this.Controls.Add(this.drawButton);
            this.Name = "Interface";
            this.Text = "UNO";
            this.Load += new System.EventHandler(this.Interface_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button drawButton;
        private System.Windows.Forms.Button testButton;
        private System.Windows.Forms.FlowLayoutPanel cards;
        private System.Windows.Forms.Label colorLabel;
        private System.Windows.Forms.FlowLayoutPanel players;
        private System.Windows.Forms.Label playersLabel;
        private System.Windows.Forms.Label turnDirection;
        private System.Windows.Forms.Label cardsLabel;
        private System.Windows.Forms.Button bluffButton;
        private System.Windows.Forms.ListBox chat;
    }
}

