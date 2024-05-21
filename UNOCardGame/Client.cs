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
        /// Funzione che chiude la UI.
        /// </summary>
        public IProgress<string> CloseUI { get; set; } = null;

        /// <summary>
        /// Logger del client
        /// </summary>
        private Logger Log = new("CLIENT");

        /// <summary>
        /// Permette di mandare dati al Sender per mandare pacchetti
        /// </summary>
        private ChannelWriter<ChannelData> Writer;

        private CancellationTokenSource ListenerCancellation;
        private CancellationTokenSource SenderCancellation;

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
            // Connette il socket
            var connectTask = new Task(async () => await Connect());
            connectTask.Start();
            connectTask.Wait();
            if (connectTask.IsCompletedSuccessfully)
            {
                // Manda richiesta di Join
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
        public void Close(bool abandon)
        {
            if (ServerSocket != null)
            {
                Send(new ConnectionEnd(abandon));

                ListenerCancellation.Cancel();
                Log.Info("Chiusura listener handler...");
                if (!ListenerHandler.IsCompleted)
                    ListenerHandler.Wait();

                //SenderCancellation.Cancel();
                Log.Info("Chiusura sender handler...");
                if (!SenderHandler.IsCompleted)
                    SenderHandler.Wait();

                ServerSocket.Close();
                ServerSocket = null;
                // TODO: Salvataggio partita nella lista delle partite fatte
            }
        }

        /// <summary>
        /// Manda i pacchetti al server
        /// </summary>
        /// <param name="read">Canale da cui riceve i pacchetti da mandare</param>
        /// <returns></returns>
        private async Task Sender(ChannelReader<ChannelData> read, CancellationToken canc)
        {
            while (true)
            {
                try
                {
                    canc.ThrowIfCancellationRequested();
                    var data = await read.ReadAsync(canc);
                    switch ((PacketType)data.PacketId)
                    {
                        case PacketType.ConnectionEnd:
                            await Packet.Send(ServerSocket, (ConnectionEnd)data.Data);
                            Log.Info("Mandato pacchetto per la disconnessione.");
                            return;
                        case PacketType.ChatMessage:
                            await Packet.Send(ServerSocket, (ChatMessage)data.Data);
                            Log.Info("Chat Message sent to Server");
                            break;
                    }
                }
                catch (PacketException e)
                {
                    Log.Error($"Packet exception occurred: {e}");
                    CloseUI.Report($"Packet exception occurred: {e}");
                    return;
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Closing sender...");
                    return;
                }
                catch (Exception e)
                {
                    Log.Error($"Exception occurred: {e}");
                    CloseUI.Report($"Exception occurred: {e}");
                    return;
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
        private async Task Listener(CancellationToken canc)
        {
            // Lista dei player nel gioco
            Dictionary<uint, Player> players = new();

            string errMsg = "Errore durante la ricezione dei pacchetti dal server: ";
            while (true)
            {
                try
                {
                    string packetString = "(None)";
                    short packetType = await Packet.ReceiveType(ServerSocket, canc);
                    switch ((PacketType)packetType)
                    {
                        case PacketType.ConnectionEnd:
                            await Packet.CancelReceive(ServerSocket);
                            
                            return;
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
                                            // Un Player ID non esistente non è valido
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
                        case PacketType.PlayerUpdate:
                            {
                                var packet = await Packet.Receive<PlayersUpdate>(ServerSocket);

                                if (packet.Players.Count == 0)
                                {
                                    Log.Warn("Server sent an invalid PlayersUpdate packet");
                                    packetString = packet.Serialize();
                                    goto invalid;
                                }

                                players.Clear();
                                foreach (var player in packet.Players)
                                    if (player.Id is uint id)
                                        players[id] = player;

                                UpdatePlayers.Report(players.Values.ToList());
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
                    errMsg += $"Errore durante la ricezione di un pacchetto: {e}";
                    Log.Error(errMsg);
                    CloseUI.Report(errMsg);
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Closing listener...");
                    return;
                }
                catch (Exception e)
                {
                    errMsg += $"Errore: {e}";
                    Log.Error(errMsg);
                    CloseUI.Report(errMsg);
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
            try
            {
                if (ServerIP != null)
                    ip = IPAddress.Parse(ServerIP);
                else if (ServerDNS != null)
                    ip = (await Dns.GetHostEntryAsync(ServerDNS)).AddressList[0];
                else throw new ArgumentNullException(nameof(ip), "Il DNS o l'IP devono essere specificati.");
            }
            catch (Exception e)
            {
                string errMsg = $"IP o DNS non valido: {e}";
                CloseUI.Report(errMsg);
                return;
            }

            // Crea il nuovo socket e si connette al server
            IPEndPoint endPoint;
            try
            {
                endPoint = new(ip, ServerPort);
                ServerSocket = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                ServerSocket.ReceiveTimeout = -1;
                ServerSocket.SendTimeout = TimeOutMillis;
                await ServerSocket.ConnectAsync(endPoint);
            }
            catch (Exception e)
            {
                string errMsg = $"Impossibile connettersi al server: {e}";
                CloseUI.Report(errMsg);
                return;
            }
            Log.Info($"Connected to server: {endPoint}");
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
                var responseType = await Packet.ReceiveType(ServerSocket, null);
                if (responseType == (short)PacketType.JoinStatus)
                {
                    var status = await Packet.Receive<JoinStatus>(ServerSocket);
                    if (status.Player is var playerData && status.AccessCode is long accessCode)
                    {
                        // Salva i dati del player
                        Player = playerData;
                        AccessCode = accessCode;

                        // Avvia gli handler della connessione
                        StartHandlers();
                        return;
                    }
                    else if (status.Err is var error)
                        msg = $"Richiesta per unirsi al server non riuscita: {error}";
                    else
                        msg = $"Status della connessione mandato dal server non valido: {status.Serialize()}";
                }
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
            if (msg != null)
                Log.Error(msg);
            CloseUI.Report(msg);
        }

        /// <summary>
        /// Manda la richiesta per riunirsi al server.
        /// </summary>
        /// <returns></returns>
        private async Task Rejoin()
        {
            string msg = "Riconnessione al server fallita: ";
            uint id;
            long accessCode;
            if (Player != null)
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
                var responseType = await Packet.ReceiveType(ServerSocket, null);
                if (responseType == (short)PacketType.JoinStatus)
                {
                    var status = await Packet.Receive<JoinStatus>(ServerSocket);
                    if (status.AccessCode is long newAccessCode)
                    {
                        // Salva il nuovo access code
                        AccessCode = newAccessCode;
                        StartHandlers();
                        return;
                    }
                    else if (status.Err is string error)
                        msg += $"La richiesta di riconnessione non è stata accettata: {error}";
                    else
                        msg += $"Status della connessione mandato dal server non valido: {status.Serialize()}";
                }
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    msg = "Il server ha chiuso la connessione";
                else
                    msg = $"Errore di comunicazione dei pacchetti durante la connessione al server: {e}";
            }

        close:
            Log.Error(msg);
            CloseUI.Report(msg);
        }
    }
}
