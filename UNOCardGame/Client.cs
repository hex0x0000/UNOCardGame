using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UNOCardGame.Packets;

namespace UNOCardGame
{
    /// <summary>
    /// Informazioni del messaggio che verrà visualizzato nella UI.
    /// </summary>
    public struct MessageDisplay
    {
        public MessageDisplay(Color nameColor, string name, string message)
        {
            NameColor = nameColor; Name = name; Message = message;
        }
        public readonly Color NameColor;
        public readonly string Name;
        public readonly string Message;
    }

    public class Client
    {
        private int _RunFlag;

        /// <summary>
        /// Il client continua ad ascoltare pacchetti finché questa flag è true
        /// </summary>
        private bool RunFlag
        {
            get => (Interlocked.CompareExchange(ref _RunFlag, 1, 1) == 0); set
            {
                if (value) Interlocked.CompareExchange(ref _RunFlag, 1, 0);
                else Interlocked.CompareExchange(ref _RunFlag, 0, 1);
            }
        }

        /// <summary>
        /// IP del Server. Null se l'address è un domain.
        /// </summary>
        private string ServerIP = null;

        /// <summary>
        /// DNS del Server. Null se l'address è un IP.
        /// </summary>
        private string ServerDNS = null;

        /// <summary>
        /// Porta del server
        /// </summary>
        private ushort ServerPort;

        /// <summary>
        /// Connessione con il server.
        /// </summary>
        private Socket ServerSocket = null;

        /// <summary>
        /// Classe Player del client.
        /// </summary>
        public Player Player { get; private set; }

        /// <summary>
        /// AccessCode necessario in caso di riconnessione.
        /// </summary>
        private ulong AccessCode;

        private Task ListenerHandler;

        /// <summary>
        /// Funzione che aggiunge il messaggio alla chat nell'UI
        /// </summary>
        public IProgress<MessageDisplay> AddMsg { get; set; } = null;

        /// <summary>
        /// Inizializza il client con i dati.
        /// Il client deve essere fatto partire con Start()
        /// </summary>
        /// <param name="address">Address del server</param>
        /// <param name="port">Porta del server</param>
        /// <param name="isDNS">Specifica se l'address è un IP o un DNS</param>
        public Client(Player player, string address, ushort port, bool isDNS)
        {
            if (isDNS)
                ServerDNS = address;
            else
                ServerIP = address;
            ServerPort = port;
            Player = player;
        }


        public void Close()
        {
            ServerSocket.Close();
            RunFlag = false;
            if (!ListenerHandler.IsCompleted)
                ListenerHandler.Wait(1000);
            // TODO: Salvataggio partita nella lista delle partite fatte
        }

        /// <summary>
        /// Ascolta i pacchetti che arrivano dal server e li gestisce.
        /// </summary>
        /// <returns></returns>
        private async Task Listener()
        {
            // Lista dei player nel gioco
            Dictionary<uint, Player> players = new();
            while (RunFlag)
            {
                try
                {
                    short packetType;
                    try
                    {
                        packetType = await Packet.ReceiveType(ServerSocket);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Termina la funzione se il socket del server è stato chiuso
                        return;
                    }
                    switch (packetType)
                    {
                        case Packet.ServerEnd:
                            RunFlag = false;
                            // TODO: Gestire la chiusura del server
                            break;
                        case (short)PacketType.ChatMessage:
                            {
                                var packet = await Packet.Receive<ChatMessage>(ServerSocket);
                                if (packet.FromId is uint fromID)
                                {
                                    if (players.TryGetValue(fromID, out var player))
                                    {
                                        // Aggiunge il messaggio con il colore dell'username del player che l'ha mandato
                                        var msgDisplay = new MessageDisplay(player.Personalizations.UsernameColor, player.Name, packet.Message);
                                        AddMsg.Report(msgDisplay);
                                    }
                                }
                            }
                            break;
                        case (short)PacketType.PlayerUpdate:
                            {
                                var packet = await Packet.Receive<PlayerUpdate>(ServerSocket);
                                // TODO: Gestire player updates
                            }
                            break;
                        default:
                            Log.Warn($"Server sent an unknown packet: {packetType}");
                            break;
                    }
                }
                catch (PacketException e)
                {
                    Log.Info($"Packet exception occurred: {e}");
                }
                catch (Exception e)
                {
                    Log.Info($"Exception occurred: {e}");
                }
            }
        }

        /// <summary>
        /// Si connette al Server.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Questo errore avviene se non si settano né il ServerIP né il ServerDNS</exception>
        private async Task Connect()
        {
            // Se il socket del server esiste già termina la funzione
            if (ServerSocket != null)
                return;

            // Ricava l'IP del server
            IPAddress ip;
            if (ServerIP != null)
                ip = IPAddress.Parse(ServerIP);
            else if (ServerDNS != null)
                ip = (await Dns.GetHostEntryAsync(ServerDNS)).AddressList[0];
            else throw new ArgumentNullException(nameof(ip), "Either DNS or IP must be specified");

            // Crea il nuovo socket e si connette al server
            var endPoint = new IPEndPoint(ip, ServerPort);
            ServerSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await ServerSocket.ConnectAsync(endPoint);

            // Manda la richiesta per unirsi al gioco
            var joinRequest = new Join(Player);
            await Packet.Send(ServerSocket, joinRequest);

            // Riceve lo status della richiesta
            var responseType = await Packet.ReceiveType(ServerSocket);
            if (responseType == (short)PacketType.JoinStatus)
            {
                var status = await Packet.Receive<JoinStatus>(ServerSocket);
                if (status.Ok is var newPlayerData)
                {
                    Player = newPlayerData.Player;
                    AccessCode = newPlayerData.AccessCode;
                    ListenerHandler = new Task(async () => await Listener());
                    ListenerHandler.Start();
                }
                else if (status.Err is var error)
                {
                    ServerSocket.Close();
                    ServerSocket = null;
                    // TODO: Gestire l'errore
                }
            }
        }

        // TODO: Implementare Reconnect
    }
}
