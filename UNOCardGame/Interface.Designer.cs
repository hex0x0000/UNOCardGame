
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
            //this.players.Enter += new System.EventHandler(this.groupBox1_Enter);
            // 
            // draw
            // 
            this.draw.Location = new System.Drawing.Point(45, 430);
            this.draw.Name = "draw";
            this.draw.Size = new System.Drawing.Size(193, 291);
            this.draw.TabIndex = 2;
            this.draw.Text = "Pesca";
            this.draw.UseVisualStyleBackColor = true;
            //this.draw.Click += new System.EventHandler(this.button1_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(45, 87);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(193, 304);
            this.button3.TabIndex = 4;
            this.button3.Text = "Carta sul Tavolo";
            this.button3.UseVisualStyleBackColor = true;
            //this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // cards
            // 
            this.cards.Location = new System.Drawing.Point(353, 87);
            this.cards.Name = "cards";
            this.cards.Size = new System.Drawing.Size(842, 634);
            this.cards.TabIndex = 5;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1207, 733);
            this.Controls.Add(this.cards);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.draw);
            this.Controls.Add(this.players);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox players;
        private System.Windows.Forms.Button draw;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.FlowLayoutPanel cards;
    }
}

