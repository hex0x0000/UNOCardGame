using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace UNOCardGame
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectForm : Form
    {
        private bool Started = false;
        private MainGame Game;

        public ConnectForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Inizia il gioco connettendosi a un server
        /// </summary>
        private void connect_Click(object sender, EventArgs e)
        {
            StartGame(isDNS.Checked);
        }

        /// <summary>
        /// Inizia il gioco hostando il server
        /// </summary>
        private void host_Click(object sender, EventArgs e)
        {
            StartGame(null);
        }

        /// <summary>
        /// Inizia il gioco.
        /// Il motivo per cui il server non può avere "isDNS" è perché esso può ascoltare solo da IP locali.
        /// </summary>
        /// <param name="_isDNS"></param>
        private void StartGame(bool? _isDNS)
        {
            if (!Started)
            {
                Started = true;
                try
                {
                    if (_isDNS is bool isDNS)
                        // Inizia il gioco come client
                        Game = new MainGame(new Player(nickname.Text), address.Text, ushort.Parse(port.Text), isDNS);
                    else
                        // Inizia il gioco come server (host)
                        Game = new MainGame(new Player(nickname.Text), address.Text, ushort.Parse(port.Text));
                    Game.FormClosed += (sender, e) =>
                    {
                        Started = false;
                        Show();
                    };
                    Game.Show();
                }
                catch (OverflowException)
                {
                    MessageBox.Show("La porta deve essere valida");
                    Started = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante l'apertura del gioco: {ex}");
                    Started = false;
                }
            }
            if (Started)
                Hide();
        }
    }
}
