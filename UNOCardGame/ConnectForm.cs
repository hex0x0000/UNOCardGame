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
        private long? PrevAccessCode = null;
        private Player PrevUser = null;
        private readonly Personalization personalizations = null;
        private readonly ColorDialog colorDialog = null;

        public ConnectForm()
        {
            InitializeComponent();
            personalizations = new();
            colorDialog = new()
            {
                FullOpen = false
            };
        }

        /// <summary>
        /// Inizia il gioco connettendosi a un server
        /// </summary>
        private void connect_Click(object sender, EventArgs e)
        {
            StartGame(isDNS.Checked, null, null);
        }

        /// <summary>
        /// Inizia il gioco hostando il server
        /// </summary>
        private void host_Click(object sender, EventArgs e)
        {
            StartGame(null, null, null);
        }

        private void reconnect_Click(object sender, EventArgs e)
        {
            if (PrevUser is Player prevUser && PrevAccessCode is long prevAccessCode)
            {
                reconnect.Enabled = false;
                StartGame(isDNS.Checked, prevUser, prevAccessCode);
            }
        }

        /// <summary>
        /// Inizia il gioco.
        /// Il motivo per cui il server non può avere "isDNS" è perché esso può ascoltare solo da IP locali.
        /// </summary>
        /// <param name="isDNS"></param>
        private void StartGame(bool? isDNS, Player player, long? accessCode)
        {
            if (!Started)
            {
                Started = true;
                try
                {
                    if (isDNS is bool __isDNS && player is Player _player && accessCode is long _accessCode)
                        // Si riconnette a una partita precedente
                        Game = new MainGame(_player, address.Text, ushort.Parse(port.Text), __isDNS, _accessCode);
                    else if (isDNS is bool _isDNS)
                        // Inizia il gioco come client
                        Game = new MainGame(new Player(nickname.Text, personalizations), address.Text, ushort.Parse(port.Text), _isDNS);
                    else
                        // Inizia il gioco come server (host)
                        Game = new MainGame(new Player(nickname.Text, personalizations), address.Text, ushort.Parse(port.Text));
                    Game.FormClosed += (sender, e) =>
                    {
                        PrevUser = Game.Client.Player;
                        PrevAccessCode = Game.Client.AccessCode;
                        if (PrevUser != null && PrevAccessCode != null)
                            reconnect.Enabled = true;
                        Started = false;
                        Show();
                    };
                    Game.Show();
                }
                catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                {
                    MessageBox.Show("La porta deve essere valida (numero compreso tra 1 e 65535)");
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

        private void selectName_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                personalizations.UsernameColor = new PlayerColor(colorDialog.Color);
                ShowPlayerPreview();
            }
        }

        private void selectBg_Click(object sender, EventArgs e)
        {
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                personalizations.BackgroundColor = new PlayerColor(colorDialog.Color);
                ShowPlayerPreview();
            }
        }

        private void ShowPlayerPreview()
        {
            if (nickname.Text != "")
            {
                labelPanel.Controls.Clear();
                var player = new Player(nickname.Text, personalizations);
                player.IsOnline = true;
                labelPanel.Controls.Add(player.GetAsLabel(false, 0));
            }
        }

        private void nickname_TextChanged(object sender, EventArgs e)
        {
            ShowPlayerPreview();
        }
    }
}
