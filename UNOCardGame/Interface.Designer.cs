
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
            this.players = new System.Windows.Forms.GroupBox();
            this.draw = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.cards = new System.Windows.Forms.FlowLayoutPanel();
            this.colorLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // players
            // 
            this.players.Location = new System.Drawing.Point(12, 12);
            this.players.Name = "players";
            this.players.Size = new System.Drawing.Size(1183, 69);
            this.players.TabIndex = 0;
            this.players.TabStop = false;
            this.players.Text = "Giocatori";
            // 
            // draw
            // 
            this.draw.Location = new System.Drawing.Point(45, 430);
            this.draw.Name = "draw";
            this.draw.Size = new System.Drawing.Size(193, 291);
            this.draw.TabIndex = 2;
            this.draw.Text = "Pesca";
            this.draw.UseVisualStyleBackColor = true;
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(45, 87);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(193, 304);
            this.button3.TabIndex = 4;
            this.button3.Text = "Carta sul Tavolo";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // cards
            // 
            this.cards.Location = new System.Drawing.Point(353, 116);
            this.cards.Name = "cards";
            this.cards.Size = new System.Drawing.Size(842, 605);
            this.cards.TabIndex = 5;
            // 
            // colorLabel
            // 
            this.colorLabel.AutoSize = true;
            this.colorLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            this.colorLabel.Location = new System.Drawing.Point(348, 87);
            this.colorLabel.Name = "colorLabel";
            this.colorLabel.Size = new System.Drawing.Size(88, 26);
            this.colorLabel.TabIndex = 6;
            this.colorLabel.Text = "Colore: ";
            // 
            // Interface
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1207, 733);
            this.Controls.Add(this.colorLabel);
            this.Controls.Add(this.cards);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.draw);
            this.Controls.Add(this.players);
            this.Name = "Interface";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Interface_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox players;
        private System.Windows.Forms.Button draw;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.FlowLayoutPanel cards;
        private System.Windows.Forms.Label colorLabel;
    }
}

