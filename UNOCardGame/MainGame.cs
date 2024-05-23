using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UNOCardGame.Packets;

namespace UNOCardGame
{
    [SupportedOSPlatform("windows")]
    public partial class MainGame : Form
    {
        /// <summary>
        /// Il client di questo gioco.
        /// </summary>
        public Client Client = null;

        /// <summary>
        /// Il server di questo gioco (solo se si sta hostando la partita)
        /// </summary>
        private Server Server = null;

        /// <summary>
        /// Se la chiusura è stata forzata, specifica se successivamente è possibile riunirsi o no
        /// </summary>
        private bool? Abandon = null;

        /// <summary>
        /// Font normale della chat
        /// </summary>
        private Font _ChatNormal;

        /// <summary>
        /// Font della chat in italic
        /// </summary>
        private Font _ChatItalic;

        /// <summary>
        /// Font della chat in bold
        /// </summary>
        private Font _ChatBold;

        /// <summary>
        /// Inizializza i componenti legati allo stile della UI
        /// </summary>
        private void InitStyleComponents()
        {
            _ChatNormal = chat.Font;
            _ChatItalic = new Font(chat.Font, FontStyle.Italic);
            _ChatBold = new Font(chat.Font, FontStyle.Bold);
        }

        private void InitClient(Player player, string address, ushort port, bool isDNS, long? prevAccessCode)
        {
            if (prevAccessCode is long _prevAccessCode)
                Client = new Client(player, address, port, isDNS, _prevAccessCode);
            else
                Client = new Client(player, address, port, isDNS);

            // Progress che fa visualizzare i nuovi messaggi
            var appendMsg = new Progress<MessageDisplay>();
            appendMsg.ProgressChanged += (s, message) => AppendMessage(message);
            Client.AddMsg = appendMsg;

            // Progress che fa visualizzare i nuovi player
            var playerUpdate = new Progress<List<Player>>();
            playerUpdate.ProgressChanged += (s, message) => DisplayPlayers(message);
            Client.UpdatePlayers = playerUpdate;

            // Progress che chiude il gioco
            var close = new Progress<(string, bool)>();
            close.ProgressChanged += (s, message) => ForceCloseGame(message.Item1, message.Item2);
            Client.ForceClose = close;
        }

        /// <summary>
        /// Si riconnette a un server
        /// </summary>
        /// <param name="player"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="isDNS"></param>
        /// <param name="prevAccessCode"></param>
        public MainGame(Player player, string address, ushort port, bool isDNS, long prevAccessCode)
        {
            InitializeComponent();
            InitStyleComponents();
            InitClient(player, address, port, isDNS, prevAccessCode);
        }

        /// <summary>
        /// Si connette a un server
        /// </summary>
        /// <param name="player"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="isDNS"></param>
        public MainGame(Player player, string address, ushort port, bool isDNS)
        {
            InitializeComponent();
            InitStyleComponents();
            InitClient(player, address, port, isDNS, null);
        }

        /// <summary>
        /// Hosta il server
        /// </summary>
        /// <param name="player"></param>
        /// <param name="address"></param>
        /// <param name="port"></param>
        public MainGame(Player player, string address, ushort port)
        {
            InitializeComponent();
            InitStyleComponents();
            InitClient(player, address, port, false, null);
            Server = new Server(address, port);
        }

        private void Interface_Load(object sender, EventArgs e)
        {
            Enabled = false;
            long? hostAccessCode = null;
            if (Server is Server server)
            {
                try
                {
                    hostAccessCode = server.Start(Client.Player);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Impossibile far partire il server: {ex.Message}");
                    Abandon = true;
                    Close();
                    return;
                }
                ServiceMessage("Avvio del server riuscito.");
            }

            try
            {
                if (Client.Start(hostAccessCode))
                {
                    ServiceMessage($"Connessione al server ({((Client.ServerDNS != null) ? Client.ServerDNS : Client.ServerIP)}:{Client.ServerPort}) riuscita.");
                    Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossibile far partire il client: {ex.Message}");
                if (Server != null)
                    Server.Stop();
                Abandon = false;
                Close();
            }
        }

        private void msgSendButton_Click(object sender, EventArgs e)
        {
            string msg = msgWriteBox.Text;
            if (msg == "")
                return;
            msgWriteBox.Clear();
            Client.Send(new ChatMessage(msg));
        }

        /// <summary>
        /// Messaggio di servizio messo nella chat dal client stesso.
        /// </summary>
        /// <param name="msg">Messaggio di servizio</param>
        private void ServiceMessage(string msg)
        {
            chat.SelectionStart = chat.TextLength;
            chat.SelectionLength = 0;
            chat.SelectionFont = _ChatItalic;
            chat.SelectionColor = Color.DimGray;
            chat.AppendText(msg + Environment.NewLine);
        }

        /// <summary>
        /// Aggiunge alla chat un messaggio ricevuto dal server
        /// </summary>
        /// <param name="msg">Messaggio ricevuto dal server</param>
        private void AppendMessage(MessageDisplay msg)
        {
            chat.SelectionStart = chat.TextLength;
            chat.SelectionLength = 0;
            if (msg.Name is var name && msg.NameColor is Color nameColor)
            {
                chat.SelectionColor = nameColor;
                chat.SelectionFont = _ChatBold;
                chat.AppendText(name);
                chat.SelectionColor = chat.ForeColor;
                chat.AppendText(": ");
                chat.SelectionFont = _ChatNormal;
            }
            else
            {
                chat.SelectionFont = _ChatItalic;
                chat.SelectionColor = Color.DimGray;
            }
            chat.AppendText(msg.Message + Environment.NewLine);
        }

        /// <summary>
        /// Mostra la lista dei player aggiornata
        /// </summary>
        /// <param name="_players"></param>
        private void DisplayPlayers(List<Player> _players)
        {
            players.Controls.Clear();
            foreach (var player in _players)
                players.Controls.Add(player.GetAsLabel(false));
        }

        /// <summary>
        /// Chiude il gioco in maniera forzata, riportando errori in caso ci siano stati
        /// </summary>
        void ForceCloseGame(string errMsg, bool abandon)
        {
            Abandon = abandon;
            Enabled = false;
            if (errMsg != null)
                MessageBox.Show(errMsg);
            Close();
        }

        /// <summary>
        /// Comportamento alla chiusura
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Enabled = false;
            if (Server == null)
            {
                if (Abandon is bool abandon)
                    Client.Close(abandon);
                else if (MessageBox.Show("Se clicchi 'No' potrai riunirti successivamente", "Vuoi chiudere definitivamente la connessione?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Client.Close(true);
                else Client.Close(false);
            }
            else Client.Close(true);
            if (Server is Server server)
                server.Stop();
            base.OnFormClosing(e);
        }
    }
}
