using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Channels;
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

    [SupportedOSPlatform("windows")]
    public class Client
    {
        private int _RunFlag;

        /// <summary>
        /// Il client continua ad ascoltare pacchetti finché questa flag è true
        /// </summary>
        private bool RunFlag
        {
            get => (Interlocked.CompareExchange(ref _RunFlag, 1, 1) == 1); set
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
        public string ServerIP { get; } = null;

        /// <summary>
        /// DNS del Server. Null se l'address è un IP.
        /// </summary>
        public string ServerDNS { get; } = null;

        /// <summary>
        /// Porta del server
        /// </summary>
        public ushort ServerPort { get; } = 0;

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
        /// Task che manda i pacchetti al server.
        /// </summary>
        private Task SenderHandler;

        /// <summary>
        /// Funzione che aggiunge il messaggio alla chat nell'UI
        /// </summary>
        public IProgress<MessageDisplay> AddMsg { get; set; } = null;

        /// <summary>
        /// Funzione che aggiorna i player nella UI.
        /// </summary>
        public IProgress<List<Player>> UpdatePlayers { get; set; } = null;

        /// <summary>
        /// Logger del client
        /// </summary>
        private Logger Log = new("CLIENT");

        /// <summary>
        /// Permette di mandare dati al Sender per mandare pacchetti
        /// </summary>
        private ChannelWriter<ChannelData> Writer;

        /// <summary>
        /// Dati mandati tramite il Writer
        /// </summary>
        private struct ChannelData
        {
            public ChannelData(short id, object data)
            {
                PacketId = id; Data = data;
            }

            /// <summary>
            /// PacketId di Data.
            /// </summary>
            public readonly short PacketId;

            /// <summary>
            /// Contenuto del pacchetto.
            /// </summary>
            public readonly object Data;
        }

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
            RunFlag = true;
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
            if (ServerSocket != null)
            {
                ServerSocket.Close();
                RunFlag = false;
                if (ListenerHandler != null)
                    if (!ListenerHandler.IsCompleted)
                        ListenerHandler.Wait(1000);
                // TODO: Salvataggio partita nella lista delle partite fatte
            }
        }

        /// <summary>
        /// Manda i pacchetti al server
        /// </summary>
        /// <param name="read">Canale da cui riceve i pacchetti da mandare</param>
        /// <returns></returns>
        private async Task Sender(ChannelReader<ChannelData> read)
        {
            while (RunFlag)
            {
                try
                {
                    var data = await read.ReadAsync();
                    switch (data.PacketId)
                    {
                        case (short)PacketType.ChatMessage:
                            await Packet.Send(ServerSocket, (ChatMessage)data.Data);
                            Log.Info("Chat Message sent to Server");
                            break;
                    }
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
        /// Funzione che permette alla UI di mandare pacchetti al server
        /// </summary>
        /// <typeparam name="T">Serialization<T></typeparam>
        /// <param name="packet">Pacchetto qualsiasi, supportato dal Sender</param>
        public bool Send<T>(T packet) where T : Serialization<T> => Writer.TryWrite(new ChannelData(packet.PacketId, packet));

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
                                            var msgDisplay = new MessageDisplay(player.Personalizations.UsernameColor.ToColor(), player.Name, msg);
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
            Log.Info($"Connected to server: {endPoint}");
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
            try
            {
                var responseType = await Packet.ReceiveType(ServerSocket);
                if (responseType == (short)PacketType.JoinStatus)
                {
                    var status = await Packet.Receive<JoinStatus>(ServerSocket);
                    if (status.Player is var playerData && status.AccessCode is long accessCode)
                    {
                        // Salva i dati del player
                        Player = playerData;
                        AccessCode = accessCode;

                        // Crea il canale di comunicazione con il Sender
                        Channel<ChannelData> channel = Channel.CreateUnbounded<ChannelData>();
                        Writer = channel.Writer;

                        // Avvia i task della connessione
                        ListenerHandler = new Task(async () => await Listener());
                        ListenerHandler.Start();
                        SenderHandler = new Task(async () => await Sender(channel.Reader));
                        SenderHandler.Start();
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
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                {
                    Log.Info("Closing client...");
                    return;
                }
                Log.Error($"A packet exception occurred: {e}");
            }
            catch (Exception e)
            {
                Log.Error($"An exception occurred: {e}");
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
