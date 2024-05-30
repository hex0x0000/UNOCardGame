using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    [SupportedOSPlatform("windows")]
    public partial class ServerOptions : Form
    {
        /// <summary>
        /// Numero massimo di player possibili nel server
        /// </summary>
        public int MaxPlayers { get; private set; } = 0;

        /// <summary>
        /// Messaggio mostrato all'entrata del server
        /// </summary>
        public string MotdMessage { get; private set; } = "";

        public static ServerOptions ShowOptions()
        {
            var serverOptions = new ServerOptions();
            serverOptions.ShowDialog();
            return serverOptions;
        }

        private ServerOptions()
        {
            InitializeComponent();
        }

        private void ok_Click(object sender, EventArgs e)
        {
            try
            {
                MaxPlayers = int.Parse(maxPlayers.Text);
            }
            catch (Exception ex) when (ex is FormatException || ex is OverflowException)
            {
                MessageBox.Show("Devi inserire un numero di player valido.");
                return;
            }

            if (motd.Text == "")
            {
                MessageBox.Show("Devi inserire un messaggio di entrata");
                return;
            }
            else MotdMessage = motd.Text;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (MaxPlayers == 0 || MotdMessage == "")
            {
                MessageBox.Show("Devi inserire i dati richiesti.");
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
