using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
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
        public MessageDisplay(Color? nameColor, string name, string message)
        {
            NameColor = nameColor; Name = name; Message = message;
        }
        public readonly Color? NameColor;
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
        /// Timeout della connessione.
        /// </summary>
        private const int TimeOutMillis = 20 * 1000;

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
        private ushort ServerPort = 0;

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
        private long? AccessCode = null;

        /// <summary>
        /// Handler del task che ascolta le richieste del server.
        /// </summary>
        private Task ListenerHandler;

        /// <summary>
        /// Funzione che aggiunge il messaggio alla chat nell'UI
        /// </summary>
        public IProgress<MessageDisplay> AddMsg { get; set; } = null;

        /// <summary>
        /// Funzione che aggiorna i player nella UI.
        /// </summary>
        public IProgress<List<Player>> UpdatePlayers { get; set; } = null;

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

        /// <summary>
        /// Avvia il client.
        /// </summary>
        public void Start()
        {
            var connectTask = new Task(async () => await Connect());
            connectTask.Start();
            connectTask.Wait();
            if (connectTask.IsCompletedSuccessfully)
            {
                var joinHandler = (AccessCode == null) ? new Task(async () => await Join()) : new Task(async () => await Rejoin());
                joinHandler.Start();
                joinHandler.Wait();
                if (joinHandler.IsFaulted)
                    throw joinHandler.Exception;
            }
            else if (connectTask.IsFaulted)
                throw connectTask.Exception;
        }

        /// <summary>
        /// Termina il client.
        /// </summary>
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
                    string packetString = "(None)";
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
                                if (packet.Message is var msg)
                                {
                                    if (packet.FromId is uint fromID)
                                    {
                                        if (players.TryGetValue(fromID, out var player))
                                        {
                                            // Aggiunge il messaggio con il colore dell'username del player che l'ha mandato
                                            var msgDisplay = new MessageDisplay(player.Personalizations.UsernameColor, player.Name, msg);
                                            AddMsg.Report(msgDisplay);
                                        }
                                        else
                                        {
                                            // Un Player ID non esistente non è valido
                                            packetString = packet.Serialize();
                                            goto invalid;
                                        }
                                    }
                                    else
                                    {
                                        // Un messaggio senza user è un messaggio di servizio del server
                                        var msgDisplay = new MessageDisplay(null, null, msg);
                                        AddMsg.Report(msgDisplay);
                                    }
                                }
                                else
                                {
                                    // Un chat message senza messaggio non è valido
                                    packetString = packet.Serialize();
                                    goto invalid;
                                }
                            }
                            continue;
                        case (short)PacketType.PlayerUpdate:
                            {
                                var packet = await Packet.Receive<PlayerUpdate>(ServerSocket);
                                switch (packet.Type)
                                {
                                    case PlayerUpdateType.PlayersUpdate:
                                        {
                                            if (packet.Players is List<Player> _players)
                                                foreach (var player in _players)
                                                    if (player.Id is uint id)
                                                    {
                                                        players.Add(id, player);
                                                        goto updatePlayersUI;
                                                    }
                                        }
                                        goto default;
                                    case PlayerUpdateType.NewPlayer:
                                        {
                                            if (packet.Player is Player newPlayer)
                                                if (newPlayer.Id is uint id)
                                                {
                                                    players.Add(id, newPlayer);
                                                    goto updatePlayersUI;
                                                }
                                        }
                                        goto default;
                                    case PlayerUpdateType.OnlineStatusUpdate:
                                        {
                                            if (packet.Id is uint id && packet.IsOnline is bool isOnline)
                                                if (players.TryGetValue(id, out var player))
                                                {
                                                    player.IsOnline = isOnline;
                                                    goto updatePlayersUI;
                                                }
                                        }
                                        goto default;
                                    case PlayerUpdateType.CardsNumUpdate:
                                        {
                                            if (packet.Id is uint id && packet.CardsNum is int cardsNum)
                                                if (players.TryGetValue(id, out var player))
                                                {
                                                    player.CardsNum = cardsNum;
                                                    goto updatePlayersUI;
                                                }
                                        }
                                        goto default;
                                    default:
                                        Log.Warn($"Server sent an invalid PlayerUpdate packet type: {packet.Type}");
                                        packetString = packet.Serialize();
                                        goto invalid;
                                }
                            updatePlayersUI:
                                List<Player> playerSorted = players.Values.OrderBy(player => player.Id).ToList();
                                UpdatePlayers.Report(playerSorted);
                            }
                            continue;
                        default:
                            Log.Warn($"Server sent an unknown packet: {packetType}");
                            await Packet.CancelReceive(ServerSocket);
                            continue;
                    }
                invalid:
                    Log.Warn($"Server sent an invalid packet. Type: {packetType}, string: {packetString}");
                }
                catch (PacketException e)
                {
                    Log.Error($"Packet exception occurred: {e}");
                }
                catch (Exception e)
                {
                    Log.Error($"Exception occurred: {e}");
                }
            }
        }

        /// <summary>
        /// Si connette al server.
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
            else throw new ArgumentNullException(nameof(ip), "Il DNS o l'IP devono essere specificati.");

            // Crea il nuovo socket e si connette al server
            var endPoint = new IPEndPoint(ip, ServerPort);
            ServerSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ServerSocket.ReceiveTimeout = -1;
            ServerSocket.SendTimeout = TimeOutMillis;
            await ServerSocket.ConnectAsync(endPoint);
        }

        /// <summary>
        /// Manda la richiesta per unirsi alla paritita al server.
        /// </summary>
        /// <returns></returns>
        private async Task Join()
        {
            // Manda la richiesta per unirsi al gioco
            var joinRequest = new Join(Player);
            await Packet.Send(ServerSocket, joinRequest);

            // Riceve lo status della richiesta
            var responseType = await Packet.ReceiveType(ServerSocket);
            if (responseType == (short)PacketType.JoinStatus)
            {
                var status = await Packet.Receive<JoinStatus>(ServerSocket);
                if (status.Player is var playerData && status.AccessCode is long accessCode)
                {
                    Player = playerData;
                    AccessCode = accessCode;
                    ListenerHandler = new Task(async () => await Listener());
                    ListenerHandler.Start();
                }
                else if (status.Err is var error)
                {
                    ServerSocket.Close();
                    ServerSocket = null;
                    Log.Error($"Failed to connect to server: {error}");
                    // TODO: Gestire l'errore
                }
            }
        }

        /// <summary>
        /// Manda la richiesta per riunirsi al server.
        /// </summary>
        /// <returns></returns>
        private async Task Rejoin()
        {
            uint id;
            long accessCode;
            if (Player != null)
                if (Player.Id is uint _id)
                {
                    id = _id;
                    if (AccessCode is long _accessCode)
                        accessCode = _accessCode;
                    else throw new ArgumentNullException(nameof(AccessCode), "L'access code deve essere impostato per riunirsi.");
                }
                else throw new ArgumentNullException(nameof(Player.Id), "L'ID del player deve essere impostato per riunirsi.");
            else throw new ArgumentNullException(nameof(Player), "Le informazioni del player devono essere impostate per riunirsi.");

            // Manda la richiesta per riunirsi
            var rejoinRequest = new Join(id, accessCode);
            await Packet.Send(ServerSocket, rejoinRequest);

            // Riceve lo status della richiesta
            var responseType = await Packet.ReceiveType(ServerSocket);
            if (responseType == (short)PacketType.JoinStatus)
            {
                var status = await Packet.Receive<JoinStatus>(ServerSocket);
                if (status.AccessCode is long newAccessCode)
                {
                    AccessCode = newAccessCode;
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
    }
}
