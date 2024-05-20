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
        private Client Client = null;

        /// <summary>
        /// Il server di questo gioco (solo se si sta hostando la partita)
        /// </summary>
        private Server Server = null;

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

        private void InitClient(Player player, string address, ushort port, bool isDNS)
        {
            Client = new Client(player, address, port, isDNS);
            
            // Progress che fa visualizzare i nuovi messaggi
            var appendMsg = new Progress<MessageDisplay>();
            appendMsg.ProgressChanged += (s, message) => AppendMessage(message);
            Client.AddMsg = appendMsg;

            // Progress che fa visualizzare i nuovi player
            var playerUpdate = new Progress<List<Player>>();
            playerUpdate.ProgressChanged += (s, message) => DisplayPlayers(message);
            Client.UpdatePlayers = playerUpdate;
        }

        public MainGame(Player player, string address, ushort port, bool isDNS)
        {
            InitializeComponent();
            InitStyleComponents();
            InitClient(player, address, port, isDNS);
        }

        public MainGame(Player player, string address, ushort port)
        {
            InitializeComponent();
            InitStyleComponents();
            InitClient(player, address, port, false);
            Server = new Server(address, port);
        }

        private void Interface_Load(object sender, EventArgs e)
        {
            if (Server is Server server)
            {
                try
                {
                    ServiceMessage("Avvio Server...");
                    server.StartServer();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Impossibile far partire il server: {ex}");
                    Close();
                }
                ServiceMessage("Avvio riuscito.");
            }

            try
            {
                ServiceMessage($"Connessione al server ({((Client.ServerDNS != null) ? Client.ServerDNS : Client.ServerIP)}:{Client.ServerPort})...");
                Client.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossibile far partire il client: {ex}");
                if (Server != null)
                    Server.StopServer();
                Close();
            }
            ServiceMessage("Connessione riuscita.");
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
        /// Comportamento alla chiusura
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Server is Server server)
                server.StopServer();
            Client.Close();
            base.OnFormClosing(e);
        }

    }
}
