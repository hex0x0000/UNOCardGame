using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        void InitStyleComponents()
        {
            _ChatNormal = chat.Font;
            _ChatItalic = new Font(chat.Font, FontStyle.Italic);
            _ChatBold = new Font(chat.Font, FontStyle.Bold);
        }

        public MainGame(Player player, string address, ushort port, bool isDNS)
        {
            InitializeComponent();
            InitStyleComponents();
            Client = new Client(player, address, port, isDNS);
        }

        public MainGame(Player player, string address, ushort port)
        {
            InitializeComponent();
            InitStyleComponents();
            Client = new Client(player, address, port, false);
            Server = new Server(address, port);
        }

        private void Interface_Load(object sender, EventArgs e)
        {
            if (Server is var server)
            {
                try
                {
                    server.StartServer();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Impossibile far partire il server: {ex}");
                    Close();
                }
            }

            try
            {
                Client.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossibile far partire il client: {ex}");
                if (Server != null)
                    Server.StopServer();
                Close();
            }
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
        /// Aggiunge alla chat un messaggio ricevuto dal client
        /// </summary>
        /// <param name="msg">Messaggio ricevuto dal client</param>
        private void AppendClientMessage(MessageDisplay msg)
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
    }
}
