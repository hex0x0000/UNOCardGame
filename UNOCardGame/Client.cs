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
    public readonly struct MessageDisplay(Color? nameColor, string name, string message)
    {
        public readonly Color? NameColor = nameColor;
        public readonly string Name = name;
        public readonly string Message = message;
    }

    [SupportedOSPlatform("windows")]
    public class Client
    {
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
        /// Classe NewPlayer di questo giocatore.
        /// </summary>
        public Player Player { get; private set; } = null;

        /// <summary>
        /// AccessCode necessario in caso di riconnessione.
        /// </summary>
        public long? AccessCode { get; private set; } = null;

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
        public IProgress<Dictionary<uint, Player>> UpdatePlayers { get; set; } = null;

        /// <summary>
        /// Funzione che chiude il gioco.
        /// string = Messaggio visualizzato al client.
        /// bool = Dice se abbandonare o no
        /// </summary>
        public IProgress<(string, bool)> ForceClose { get; set; } = null;

        /// <summary>
        /// Aggiorna il turno
        /// </summary>
        public IProgress<TurnUpdate> TurnUpdate { get; set; } = null;

        /// <summary>
        /// Resetta il gioco e mostra la classifica
        /// </summary>
        public IProgress<GameEnd> ResetGame { get; set; } = null;

        /// <summary>
        /// Mostra un messaggio del gioco mandato dal server
        /// </summary>
        public IProgress<GameMessage> GameMessage { get; set; } = null;

        /// <summary>
        /// Logger del client.
        /// </summary>
        public Logger Log = new("CLIENT");

        /// <summary>
        /// Permette di mandare dati al Sender per mandare pacchetti
        /// </summary>
        private ChannelWriter<ChannelData> Writer;

        private CancellationTokenSource ListenerCancellation;
        private CancellationTokenSource SenderCancellation;

        private volatile int _IsSenderRunning = 0;

        /// <summary>
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool IsSenderRunning
        {
            get => (Interlocked.CompareExchange(ref _IsSenderRunning, 1, 1) == 1); set
            {
                if (value) Interlocked.CompareExchange(ref _IsSenderRunning, 1, 0);
                else Interlocked.CompareExchange(ref _IsSenderRunning, 0, 1);
            }
        }

        private volatile int _HasConnected = 0;

        /// <summary>
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool HasConnected
        {
            get => (Interlocked.CompareExchange(ref _HasConnected, 1, 1) == 1); set
            {
                if (value) Interlocked.CompareExchange(ref _HasConnected, 1, 0);
                else Interlocked.CompareExchange(ref _HasConnected, 0, 1);
            }
        }

        private volatile int _HasErrors = 0;

        /// <summary>
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool HasErrors
        {
            get => (Interlocked.CompareExchange(ref _HasErrors, 1, 1) == 1); set
            {
                if (value) Interlocked.CompareExchange(ref _HasErrors, 1, 0);
                else Interlocked.CompareExchange(ref _HasErrors, 0, 1);
            }
        }

        /// <summary>
        /// Dati mandati tramite il Writer
        /// </summary>
        private readonly struct ChannelData(short id, object data)
        {
            /// <summary>
            /// PacketId di Data.
            /// </summary>
            public readonly short PacketId = id;

            /// <summary>
            /// Contenuto del pacchetto.
            /// </summary>
            public readonly object Data = data;
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
            HasConnected = false;
            IsSenderRunning = false;
        }

        public Client(Player player, string address, ushort port, bool isDNS, long prevAccessCode)
        : this(player, address, port, isDNS)
        {
            AccessCode = prevAccessCode;
        }

        /// <summary>
        /// Avvia il client.
        /// </summary>
        public bool Start(long? hostAccessCode)
        {
            if (!HasConnected)
            {
                // Prima fase: Connette il socket
                var connectTask = new Task(async () => await Connect());
                connectTask.Start();
                connectTask.Wait();

                // Aspetta che venga stabilita la connessione con il server
                while (!HasConnected)
                    if (HasErrors)
                        return false;

                // Seconda fase: richiesta di Join
                HasConnected = false;
                if (connectTask.IsCompletedSuccessfully)
                {
                    Task joinHandler;
                    if (hostAccessCode is long _hostAccessCode)
                        // Host della partita
                        joinHandler = new Task(async () => await Rejoin(_hostAccessCode));
                    else if (AccessCode != null)
                        // Rejoin a un server
                        joinHandler = new Task(async () => await Rejoin(null));
                    else
                        // Join normale
                        joinHandler = new Task(async () => await Join());
                    joinHandler.Start();
                    joinHandler.Wait();

                    // Aspetta che il client finisca di connettersi
                    while (!HasConnected)
                        if (HasErrors)
                            return false;
                    return true;
                }
                else if (connectTask.IsFaulted)
                    throw connectTask.Exception;
                return false;
            }
            else return false;
        }

        /// <summary>
        /// Termina il client.
        /// </summary>
        public void Close(bool abandon)
        {
            if (ServerSocket != null)
            {
                if (ListenerCancellation != null)
                    ListenerCancellation.Cancel();

                if (IsSenderRunning)
                    Send(new ConnectionEnd(abandon, null));

                // Aspetta che il sender mandi la disconnessione
                while (IsSenderRunning) ;

                if (SenderCancellation != null)
                    SenderCancellation.Cancel();

                if (SenderHandler != null)
                {
                    Log.Info("Chiusura sender handler...");
                    if (!SenderHandler.IsCompleted)
                        SenderHandler.Wait();
                }

                if (ListenerHandler != null)
                {
                    Log.Info("Chiusura listener handler...");
                    if (!ListenerHandler.IsCompleted)
                        ListenerHandler.Wait();
                }

                ServerSocket.Close();
                ServerSocket = null;
                // TODO: Salvataggio partita nella lista delle partite fatte
            }
            if (abandon)
            {
                AccessCode = null;
                Player = null;
            }
        }

        /// <summary>
        /// Manda i pacchetti al server
        /// </summary>
        /// <param name="read">Canale da cui riceve i pacchetti da mandare</param>
        /// <returns></returns>
        private async Task Sender(ChannelReader<ChannelData> read, CancellationToken canc)
        {
            IsSenderRunning = true;

            try
            {
                while (true)
                {
                    canc.ThrowIfCancellationRequested();
                    var data = await read.ReadAsync(canc);
                    switch ((PacketType)data.PacketId)
                    {
                        case PacketType.ConnectionEnd:
                            await Packet.Send(ServerSocket, (ConnectionEnd)data.Data);
                            Log.Info("Mandato pacchetto per la disconnessione.");
                            IsSenderRunning = false;
                            return;
                        case PacketType.ChatMessage:
                            await Packet.Send(ServerSocket, (ChatMessage)data.Data);
                            break;
                        case PacketType.ActionUpdate:
                            await Packet.Send(ServerSocket, (ActionUpdate)data.Data);
                            break;
                        default:
                            Log.Info($"Il sender ha ricevuto un pacchetto non riconosciuto: {data.PacketId}");
                            break;
                    }
                }
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    return;
                Log.Error($"Packet exception occurred: {e}");
                ForceClose.Report(($"Packet exception occurred: {e}", false));
            }
            catch (OperationCanceledException)
            {
                Log.Info("Closing sender...");
            }
            catch (Exception e)
            {
                Log.Error($"Exception occurred: {e}");
                ForceClose.Report(($"Exception occurred: {e}", false));
            }
            finally
            {
                IsSenderRunning = false;
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
        private async Task Listener(CancellationToken canc)
        {
            // Lista dei player nel gioco
            Dictionary<uint, Player> players = new();

            string errMsg = "Errore durante la ricezione dei pacchetti dal server: ";
            try
            {
                while (true)
                {

                    string packetString = "(None)";
                    short packetType = await Packet.ReceiveType(ServerSocket, canc);
                    switch ((PacketType)packetType)
                    {
                        case PacketType.ConnectionEnd:
                            {
                                var packet = await Packet.Receive<ConnectionEnd>(ServerSocket);
                                ForceClose.Report((packet.Message, packet.Final));
                                return;
                            }
                        case PacketType.GameEnd:
                            {
                                var packet = await Packet.Receive<GameEnd>(ServerSocket);
                                ResetGame.Report(packet);
                            }
                            continue;
                        case PacketType.ChatMessage:
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
                                            Log.Info($"[CHAT] {player.Name}: {msg}");
                                        }
                                        else
                                        {
                                            // Un NewPlayer ID non esistente non è valido
                                            packetString = packet.Serialize();
                                            goto invalid;
                                        }
                                    }
                                    else
                                    {
                                        // Un messaggio senza user è un messaggio di servizio del server
                                        var msgDisplay = new MessageDisplay(null, null, msg);
                                        Log.Info($"[CHAT] Server: {msg}");
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
                        case PacketType.GameMessage:
                            {
                                var packet = await Packet.Receive<GameMessage>(ServerSocket);
                                GameMessage.Report(packet);
                            }
                            continue;
                        case PacketType.PlayerUpdate:
                            {
                                var packet = await Packet.Receive<PlayersUpdate>(ServerSocket);

                                if (packet.Players is List<Player> _players)
                                {
                                    if (_players.Count == 0)
                                    {
                                        Log.Warn("Server sent an invalid PlayersUpdate packet");
                                        packetString = packet.Serialize();
                                        goto invalid;
                                    }

                                    players.Clear();
                                    foreach (var player in _players)
                                        if (player.Id is uint id)
                                            players[id] = player;
                                }
                                else if (packet.Id is uint playerId && packet.IsOnline is bool isOnline)
                                    if (players.TryGetValue(playerId, out var player))
                                        player.IsOnline = isOnline;

                                UpdatePlayers.Report(players);
                            }
                            continue;
                        case PacketType.TurnUpdate:
                            {
                                var packet = await Packet.Receive<TurnUpdate>(ServerSocket);
                                TurnUpdate.Report(packet);
                            }
                            continue;
                        default:
                            canc.ThrowIfCancellationRequested();
                            if (packetType != 0)
                                Log.Warn($"Server sent an unknown packet: {packetType}");
                            await Packet.CancelReceive(ServerSocket);
                            continue;
                    }
                invalid:
                    Log.Warn($"Server sent an invalid packet. Type: {packetType}, string: {packetString}");
                }
            }
            catch (PacketException e)
            {
                errMsg += $"Errore durante la ricezione di un pacchetto: {e}";
                Log.Error(errMsg);
                ForceClose.Report((errMsg, false));
            }
            catch (ObjectDisposedException)
            {
                Log.Info("Closing listener...");
            }
            catch (OperationCanceledException)
            {
                Log.Info("Closing listener...");
            }
            catch (Exception e)
            {
                errMsg += $"Errore: {e}";
                Log.Error(errMsg);
                ForceClose.Report((errMsg, false));
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
            try
            {
                if (ServerIP is string serverIP)
                    ip = IPAddress.Parse(serverIP);
                else if (ServerDNS is string serverDNS)
                {
                    Log.Info($"DNS del server: {serverDNS}");
                    ip = (await Dns.GetHostEntryAsync(serverDNS)).AddressList[0];
                }
                else throw new ArgumentNullException(nameof(ip), "Il DNS o l'IP devono essere specificati.");
            }
            catch (Exception e)
            {
                HasErrors = true;
                string errMsg = $"IP o DNS non valido: {e}";
                Log.Error(errMsg);
                ForceClose.Report((errMsg, true));
                return;
            }

            Log.Info($"Indirizzo IP del server: {ip}");

            // Crea il nuovo socket e si connette al server
            IPEndPoint endPoint;
            try
            {
                endPoint = new(ip, ServerPort);
                ServerSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                ServerSocket.ReceiveTimeout = -1;
                ServerSocket.SendTimeout = TimeOutMillis;
                await ServerSocket.ConnectAsync(endPoint);
                HasConnected = true;
                Log.Info($"Connesso al server: {endPoint}");
            }
            catch (Exception e)
            {
                HasErrors = true;
                string errMsg = $"Impossibile connettersi al server: {e}";
                Log.Error(errMsg);
                ForceClose.Report((errMsg, true));
                return;
            }
        }

        /// <summary>
        /// Avvia gli handler della connessione
        /// </summary>
        private void StartHandlers()
        {
            // Crea il canale di comunicazione con il Sender
            Channel<ChannelData> channel = Channel.CreateUnbounded<ChannelData>();
            Writer = channel.Writer;
            ListenerCancellation = new();
            ListenerHandler = new Task(async () => await Listener(ListenerCancellation.Token));
            ListenerHandler.Start();
            SenderCancellation = new();
            SenderHandler = new Task(async () => await Sender(channel.Reader, SenderCancellation.Token));
            SenderHandler.Start();
        }

        /// <summary>
        /// Manda la richiesta per unirsi alla paritita al server.
        /// </summary>
        /// <returns></returns>
        private async Task Join()
        {
            string msg = null;
            try
            {
                // Manda la richiesta per unirsi al gioco
                var joinRequest = new Join(Player);
                await Packet.Send(ServerSocket, joinRequest);

                // Riceve lo status della richiesta
                var packetType = await Packet.ReceiveType(ServerSocket, null);
                if (packetType == (short)PacketType.JoinStatus)
                {
                    var status = await Packet.Receive<JoinStatus>(ServerSocket);
                    if (status.Player is var playerData && status.AccessCode is long accessCode)
                    {
                        // Salva i dati del player
                        Player = playerData;
                        AccessCode = accessCode;

                        // Avvia gli handler della connessione
                        StartHandlers();
                        HasConnected = true;
                        return;
                    }
                    else if (status.Err is var error)
                        msg = $"Richiesta per unirsi al server non riuscita: {error}";
                    else
                        msg = $"Status della connessione mandato dal server non valido: {status.Serialize()}";
                }
                else msg = $"Il server ha mandato un pacchetto non valido: {packetType}";
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    msg = "Il server ha chiuso la connessione";
                else
                    msg = $"Errore di comunicazione dei pacchetti durante la connessione al server: {e}";
            }
            catch (Exception e)
            {
                msg = $"Errore durante la connessione al server: {e}";
            }
            HasErrors = true;
            if (msg != null)
                Log.Error(msg);
            ForceClose.Report((msg, true));
        }

        /// <summary>
        /// Manda la richiesta per riunirsi al server.
        /// </summary>
        /// <returns></returns>
        private async Task Rejoin(long? hostAccessCode)
        {
            string msg = "Riconnessione al server fallita: ";
            uint id;
            long accessCode;
            if (hostAccessCode is long _hostAccessCode)
            {
                id = 0;
                accessCode = _hostAccessCode;
            }
            else if (Player != null)
                if (Player.Id is uint _id)
                {
                    id = _id;
                    if (AccessCode is long _accessCode)
                        accessCode = _accessCode;
                    else
                    {
                        msg += "L'access code deve essere impostato per riunirsi.";
                        goto close;
                    }
                }
                else
                {
                    msg += "L'ID del player deve essere impostato per riunirsi.";
                    goto close;
                }
            else
            {
                msg += "Le informazioni del player devono essere impostate per riunirsi.";
                goto close;
            }

            try
            {
                // Manda la richiesta per riunirsi
                var rejoinRequest = new Join(id, accessCode);
                await Packet.Send(ServerSocket, rejoinRequest);

                // Riceve lo status della richiesta
                var packetType = await Packet.ReceiveType(ServerSocket, null);
                if (packetType == (short)PacketType.JoinStatus)
                {
                    var status = await Packet.Receive<JoinStatus>(ServerSocket);
                    if (status.AccessCode is long newAccessCode)
                    {
                        // Salva il nuovo access code
                        AccessCode = newAccessCode;
                        StartHandlers();
                        HasConnected = true;
                        return;
                    }
                    else if (status.Err is string error)
                        msg += $"La richiesta di riconnessione non è stata accettata: {error}";
                    else
                        msg += $"Status della connessione mandato dal server non valido: {status.Serialize()}";
                }
                else msg += $"Il server ha mandato un pacchetto non valido: {packetType}";
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    msg = "Il server ha chiuso la connessione";
                else
                    msg = $"Errore di comunicazione dei pacchetti durante la connessione al server: {e}";
            }

        close:
            HasErrors = true;
            Log.Error(msg);
            ForceClose.Report((msg, false));
        }
    }
}
