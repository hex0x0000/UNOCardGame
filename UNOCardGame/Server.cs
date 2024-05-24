using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using UNOCardGame.Packets;

namespace UNOCardGame
{
    [SupportedOSPlatform("windows")]
    class Server
    {
        public const int ADMIN_ID = 0;

        /// <summary>
        /// Timeout della connessione.
        /// </summary>
        private const int TimeOutMillis = 20 * 1000;

        /// <summary>
        /// Generatore di numeri casuali.
        /// </summary>
        private static Random _Random = new Random();

        private int _HasStarted = 0;

        /// <summary>
        /// Indica se il gioco è iniziato o meno.
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool HasStarted
        {
            get => (Interlocked.CompareExchange(ref _HasStarted, 1, 1) == 1); set
            {
                if (value) Interlocked.CompareExchange(ref _HasStarted, 1, 0);
                else Interlocked.CompareExchange(ref _HasStarted, 0, 1);
            }
        }

        /// <summary>
        /// Tiene conto del numero degli user id. Parte da 1 perché 0 è riservato all'host.
        /// Il numero degli ID deve essere ordinato e univoco per mantenere l'ordine dei turni.
        /// Non thread-safe, Interlocked deve essere usato per modificare la variabile.
        /// </summary>
        private long IdCount = 1;

        /// <summary>
        /// Indirizzo IP su cui ascolta il server.
        /// </summary>
        public readonly string Address;

        /// <summary>
        /// Porta su cui ascolta il server.
        /// </summary>
        public readonly ushort Port;

        /// <summary>
        /// Handler del thread che gestisce le nuove connessioni.
        /// </summary>
        private Task ListenerHandler;

        /// <summary>
        /// Handler del thread che gestisce il broadcasting.
        /// </summary>
        private Task BroadcasterHandler;

        /// <summary>
        /// Handler del thread che gestisce il gioco.
        /// </summary>
        private Task GameMasterHandler;

        /// <summary>
        /// Canale di comunicazione con il broadcaster.
        /// </summary>
        private Channel<ChannelData> BroadcasterCommunicator;

        /// <summary>
        /// Canale di comunicazione con il GameMaster
        /// </summary>
        private Channel<ChannelData> GameMasterCommunicator;

        /// <summary>
        /// Tutti i player del gioco. Questo dictionary contiene tutti i dati necessari per comunicare con i client.
        /// E' necessario accedervi con il mutex bloccato, dato che questo oggetto non è thread-safe.
        /// </summary>
        private Dictionary<uint, PlayerData> Players = new();

        /// <summary>
        /// Mutex che coordina l'accesso a Players
        /// </summary>
        private static Mutex PlayersMutex = new();

        /// <summary>
        /// I socket dei giocatori.
        /// Risiedono su un hashmap diverso per evitare dei bottleneck durante la spedizione dei pacchetti.
        /// </summary>
        private Dictionary<uint, Socket> Clients = new();

        /// <summary>
        /// Mutex che coordina l'accesso ai client.
        /// </summary>
        private static Mutex ClientsMutex = new();

        /// <summary>
        /// Socket del server. Usato per accettare le nuove connessioni.
        /// </summary>
        private Socket ServerSocket;

        /// <summary>
        /// Logger del server
        /// </summary>
        private Logger Log = new("SERVER");

        private CancellationTokenSource ListenerCancellation;

        private CancellationTokenSource BroadcasterCancellation;

        private CancellationTokenSource GameMasterCancellation;

        private volatile int _IsBroadcasterRunning = 0;

        /// <summary>
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool IsBroadcasterRunning
        {
            get => (Interlocked.CompareExchange(ref _IsBroadcasterRunning, 1, 1) == 1); set
            {
                if (value) Interlocked.CompareExchange(ref _IsBroadcasterRunning, 1, 0);
                else Interlocked.CompareExchange(ref _IsBroadcasterRunning, 0, 1);
            }
        }

        /// <summary>
        /// I dati di ogni player.
        /// Contiene il codice di accesso, il server per comunicare con il client
        /// e le informazioni del player.
        /// </summary>
        private class PlayerData
        {
            /// <summary>
            /// Constructor di PlayerData. Genera automaticamente l'access code.
            /// </summary>
            /// <param name="id"></param>
            /// <param name="client"></param>
            /// <param name="clientHandler"></param>
            /// <param name="player"></param>
            public PlayerData(uint id, Task clientHandler, Player player, CancellationTokenSource canc)
            {
                GenAccessCode();
                Deck = new Deck();
                ClientHandler = clientHandler;
                Player = new Player(id, true, player.Name, player.Personalizations);
                Cancellation = canc;
            }

            /// <summary>
            /// Crea il playerdata dell'host.
            /// </summary>
            /// <param name="player">Informazioni del player dell'host</param>
            public PlayerData(Player player)
            {
                GenAccessCode();
                Deck = new Deck();
                Player = new Player(0, false, player.Name, player.Personalizations);
                ClientHandler = null;
                Cancellation = null;
            }

            public void GenAccessCode()
            {
                byte[] buffer = new byte[sizeof(long)];
                _Random.NextBytes(buffer);
                AccessCode = BitConverter.ToInt64(buffer, 0);
            }

            /// <summary>
            /// Il codice di accesso è necessario per evitare impersonificazioni.
            /// E' necessario anche in caso di riconnessione.
            /// </summary>
            public long AccessCode { get; private set; }

            /// <summary>
            /// Task dell'handler del client.
            /// </summary>
            public Task ClientHandler { get; set; }

            /// <summary>
            /// Dati del player non legati alla connessione.
            /// </summary>
            public Player Player { get; set; }

            /// <summary>
            /// Il mazzo di carte del player
            /// </summary>
            public Deck Deck;

            /// <summary>
            /// Permette di terminare l'handler del client
            /// </summary>
            public CancellationTokenSource Cancellation;
        }

        /// <summary>
        /// Dati mandati tramite il Communicator
        /// </summary>
        private struct ChannelData
        {
            public ChannelData(short packetId, object data)
            {
                PacketId = packetId; Data = data; PlayerId = null;
            }

            public ChannelData(short packetId, object data, uint playerId)
            {
                PacketId = packetId; Data = data; PlayerId = playerId;
            }

            /// <summary>
            /// PacketId di Data.
            /// </summary>
            public readonly short PacketId;

            /// <summary>
            /// Contenuto del pacchetto.
            /// </summary>
            public readonly object Data;

            /// <summary>
            /// ID del player a cui mandare il pacchetto (opzionale).
            /// </summary>
            public readonly uint? PlayerId;
        }

        public Server(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        ~Server()
        {
            Stop();
            ListenerHandler.Dispose();
            BroadcasterHandler.Dispose();
            // TODO: Gamemaster
            foreach (var player in Players)
                player.Value.ClientHandler.Dispose();
            PlayersMutex.Dispose();
        }

        /// <summary>
        /// Avvia il server.
        /// </summary>
        /// <param name="player">Informazioni del player dell'host</param>
        /// <returns>Access code per unirsi come host</returns>
        public long Start(Player player)
        {
            Log.Info("Creazione dell'admin...");
            var playerData = new PlayerData(player);
            long accessCode = playerData.AccessCode;
            Players.Add(ADMIN_ID, playerData);

            Log.Info("Avvio socket...");
            InitSocket();

            BroadcasterCommunicator = Channel.CreateUnbounded<ChannelData>();

            Log.Info("Avvio GameMaster...");
            GameMasterCancellation = new();
            GameMasterHandler = new Task(async () => await GameMaster(GameMasterCancellation.Token));
            GameMasterHandler.Start();

            // Broadcaster
            Log.Info("Avvio broadcaster...");
            BroadcasterCancellation = new();
            BroadcasterHandler = new Task(async () => await Broadcaster(BroadcasterCancellation.Token));
            BroadcasterHandler.Start();

            // Listener
            Log.Info("Avvio listener...");
            ListenerCancellation = new();
            ListenerHandler = new Task(async () => await Listener(ListenerCancellation.Token));
            ListenerHandler.Start();

            return accessCode;
        }

        public void Stop()
        {
            // Tempo di timeout
            const int timeOut = 1000;

            // Chiude il server solo se è stato inizializzato prima
            if (ServerSocket != null)
            {
                var endTask = new Task(async () => await SendToClients(new ConnectionEnd(true, "Il server è stato chiuso.")));
                endTask.Start();
                endTask.Wait();

                // Aspetta che il broadcaster finisca di mandare tutti i messaggi
                while (IsBroadcasterRunning) ;

                ListenerCancellation.Cancel();
                Log.Info("Aspettando chiusura del listener...");
                if (!ListenerHandler.IsCompleted)
                    ListenerHandler.Wait(timeOut);

                BroadcasterCancellation.Cancel();
                Log.Info("Aspettando chiusura del broadcaster...");
                if (!BroadcasterHandler.IsCompleted)
                    BroadcasterHandler.Wait(timeOut);

                GameMasterCancellation.Cancel();
                Log.Info("Aspettando chiusura del gamemaster...");
                if (!GameMasterHandler.IsCompleted)
                    GameMasterHandler.Wait(timeOut);

                // Termina gli handler dei player
                foreach (var player in Players)
                {
                    Log.Info($"Terminazione player '{player.Value.Player.Name}'...");
                    player.Value.Cancellation.Cancel();
                    if (!player.Value.ClientHandler.IsCompleted)
                        player.Value.ClientHandler.Wait(timeOut);

                }

                // Termina i socket, se ne sono rimasti
                foreach (var client in Clients)
                {
                    if (client.Value != null)
                    {
                        Log.Info($"Chiusura socket del player {client.Key}");
                        client.Value.Close();
                    }
                }

                ServerSocket.Close();
                ServerSocket = null;
            }
        }

        private Deck GetPlayerDeck(uint id) {
            Deck deck = null;
            PlayersMutex.WaitOne();
            if (Players.TryGetValue(id, out var player))
                deck = player.Deck;
            PlayersMutex.ReleaseMutex();
            return deck;
        }

        private void SetPlayerDeck(uint id, Deck deck)
        {
            PlayersMutex.WaitOne();
            if (Players.TryGetValue(id, out var player))
                player.Deck = deck;
            PlayersMutex.ReleaseMutex();
        }

        /// <summary>
        /// Gestisce il gioco.
        /// </summary>
        /// <returns></returns>
        private async Task GameMaster(CancellationToken canc)
        {
            // Aspetta che il gioco venga fatto partire
            while (!HasStarted)
                await Task.Delay(1);

            // Stato iniziale della partita

            // Carta iniziale sul tavolo
            var tableCard = Card.PickRandom();
            // Senso di rotazione del gioco
            var turnDirection = TurnDirection.LeftToRight;
            // Player iniziale
            uint playerTurnId = ADMIN_ID;

            await SendToClients(new TurnUpdate(playerTurnId, tableCard, turnDirection));
            await SendToClients(new ChatMessage("Partita avviata!"));
            while (true)
            {
                canc.ThrowIfCancellationRequested();
                var packet = await GameMasterCommunicator.Reader.ReadAsync(canc);
                switch ((PacketType)packet.PacketId)
                {
                    case PacketType.CardsUpdate:
                        if (packet.PlayerId is uint playerId)
                        {
                            var cardsUpdate = (CardsUpdate)packet.Data;
                            if (cardsUpdate.CardID is uint cardId)
                            {
                                var playerDeck = GetPlayerDeck(playerId);
                                var playerCard = playerDeck.Get(cardId);
                                switch (playerCard.Type) {
                                    case Type.Normal:
                                        if (playerCard.NormalType == tableCard.NormalType || playerCard.Color == tableCard.Color)
                                        {
                                            if (playerCard.NormalType == Normals.Reverse)
                                                turnDirection = !turnDirection; // TODO: Implementare bene questo
                                            tableCard = playerCard;
                                            playerDeck.Remove(cardId);
                                            await SendToClient(new CardsUpdate(playerDeck.Cards), playerId);
                                            SetPlayerDeck(playerId, playerDeck);
                                            // TODO: Implementare playerTurnId precedente/successivo
                                            await SendToClients(new TurnUpdate(playerTurnId, tableCard, turnDirection));
                                            continue;
                                        }
                                        break;
                                    case Type.Special:
                                        // TODO: Implementare carte special
                                        break;
                                    default:
                                        // TODO: Implementare errori
                                        break;
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task SendToGameMaster<T>(T packet) where T: Serialization<T> {
            if (GameMasterCommunicator != null)
                await GameMasterCommunicator.Writer.WriteAsync(new ChannelData(packet.PacketId, packet));
        }

        /// <summary>
        /// Manda un pacchetto a tutti i client.
        /// </summary>
        /// <typeparam name="T">Tipo serializzabile</typeparam>
        /// <param name="packet">Un pacchetto qualsiasi</param>
        /// <returns></returns>
        private async Task BroadcastAll<T>(T packet) where T : Serialization<T>
        {
            ClientsMutex.WaitOne();
            try
            {
                foreach (var client in Clients)
                    if (client.Value != null)
                        await Packet.Send(client.Value, packet);
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    return;
                Log.Error($"Errore durante la spedizione di un pacchetto ({packet.PacketId}): {e}");
            }
            catch (Exception e)
            {
                Log.Error($"Errore durante la spedizione di un pacchetto ({packet.PacketId}): {e}");
            }
            finally
            {
                ClientsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Manda un pacchetto a un client.
        /// </summary>
        /// <typeparam name="T">Tipo serializzabile</typeparam>
        /// <param name="id">ID del player a cui mandare il pacchetto</param>
        /// <param name="packet">Un pacchetto qualsiasi</param>
        /// <returns></returns>
        private async Task BroadcastTo<T>(uint id, T packet) where T : Serialization<T>
        {
            // Manda il messaggio al player se è online, altrimenti viene ignorato
            ClientsMutex.WaitOne();
            try
            {
                if (Clients.TryGetValue(id, out var client))
                    if (client != null)
                        await Packet.Send(client, packet);
            }
            catch (PacketException e)
            {
                if (e.ExceptionType == PacketExceptions.ConnectionClosed)
                    return;
                Log.Error($"Errore durante la spedizione del pacchetto ({packet.PacketId}) all'utente con {id}: {e}");
            }
            catch (Exception e)
            {
                Log.Error($"Errore durante la spedizione del pacchetto ({packet.PacketId}) all'utente con {id}: {e}");
            }
            finally
            {
                ClientsMutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Manda pacchetto a tutti i client
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        /// <returns></returns>
        private async Task SendToClients<T>(T packet) where T : Serialization<T>
        {
            if (BroadcasterCommunicator != null)
                await BroadcasterCommunicator.Writer.WriteAsync(new ChannelData(packet.PacketId, packet));
        }

        /// <summary>
        /// Manda pacchetto a un client
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        /// <param name="sendTo"></param>
        /// <returns></returns>
        private async Task SendToClient<T>(T packet, uint sendTo) where T : Serialization<T>
        {
            if (BroadcasterCommunicator != null)
                await BroadcasterCommunicator.Writer.WriteAsync(new ChannelData(packet.PacketId, packet, sendTo));
        }

        /// <summary>
        /// Thread che manda i pacchetti ai client.
        /// A seconda della richiesta del Canale può mandarli a un utente specifico o a tutti.
        /// </summary>
        private async Task Broadcaster(CancellationToken canc)
        {
            Log.Info("Broadcaster avviato.");
            IsBroadcasterRunning = true;
            try
            {
                while (true)
                {
                    canc.ThrowIfCancellationRequested();
                    // Aspetta che gli vengano mandati nuovi pacchetti da mandare
                    var packet = await BroadcasterCommunicator.Reader.ReadAsync(canc);
                    switch ((PacketType)packet.PacketId)
                    {
                        case PacketType.ConnectionEnd:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (ConnectionEnd)packet.Data);
                                else
                                {
                                    await BroadcastAll((ConnectionEnd)packet.Data);
                                    IsBroadcasterRunning = false;
                                    return;
                                }
                            }
                            continue;
                        case PacketType.ChatMessage:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (ChatMessage)packet.Data);
                                else
                                    await BroadcastAll((ChatMessage)packet.Data);
                            }
                            break;
                        case PacketType.PlayerUpdate:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (PlayersUpdate)packet.Data);
                                else
                                    await BroadcastAll((PlayersUpdate)packet.Data);
                            }
                            break;
                        case PacketType.TurnUpdate:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (TurnUpdate)packet.Data);
                                else
                                    await BroadcastAll((TurnUpdate)packet.Data);
                            }
                            break;
                        case PacketType.CardsUpdate:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (CardsUpdate)packet.Data);
                                else
                                    await BroadcastAll((CardsUpdate)packet.Data);
                            }
                            break;
                        default:
                            Log.Warn($"Il broadcaster ha ricevuto un pacchetto non riconosciuto: {packet.PacketId}");
                            break;
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
            {
                Log.Info("Chiusura broadcaster...");
            }
            catch (Exception e)
            {
                Log.Error($"Errore durante il broadcasting di un pacchetto: {e}");
            }
            finally
            {
                IsBroadcasterRunning = false;
            }
        }

        /// <summary>
        /// Ritorna l'ID partendo dal nome.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private uint? FromName(string name)
        {
            uint? id = null;
            PlayersMutex.WaitOne();
            foreach (var player in Players)
                if (player.Value.Player.Name == name)
                    id = player.Value.Player.Id;
            PlayersMutex.ReleaseMutex();
            return id;
        }

        /// <summary>
        /// Esegue un comando scritto in chat.
        /// </summary>
        /// <param name="id">ID dell'utente che ha fatto il comando</param>
        /// <param name="msg">Contenuto del comando</param>
        /// <returns>Ritorna true se il comando può essere mandato in chat</returns>
        private async Task<bool> ExecCommand(uint id, string msg)
        {
            string[] args = msg.ToLower().Split(' ');
            if (args[0] == "uno!")
            {
                // TODO: Implementare aggiunta di carte se non si dice UNO
                return true;
            }
            // I comandi sono riservati all'admin
            if (id != ADMIN_ID)
                return true;
            switch (args[0])
            {
                case ".help":
                    await SendToClient(new ChatMessage(".help - Mostra questo messaggio\n.start - Avvia il gioco\n.kick - Disconnette un utente\n.remove - Rimuove un utente"), id);
                    return false;
                case ".start":
                    HasStarted = true;
                    await SendToClients(new ChatMessage("Avvio del gioco..."));
                    return false;
                case ".kick":
                    if (args.Length >= 2)
                    {
                        string name = args[1];
                        var _kickId = FromName(name);
                        if (_kickId is uint kickId)
                        {
                            Log.Info($"Utente da kickare: {kickId}");
                            if (kickId != ADMIN_ID)
                            {
                                await SendToClient(new ConnectionEnd(false, "Sei stato kickato, potrai riunirti in seguito"), kickId);
                                await SendToClients(new ChatMessage($"{name} è stato kickato"));
                            }
                        }
                        else
                            await SendToClient(new ChatMessage($"L'utente {name} non esiste"), id);
                    }
                    else await SendToClient(new ChatMessage("Comando .kick non valido. Uso: .kick NomePlayer"), id);
                    return false;
                case ".remove":
                    if (args.Length >= 2)
                    {
                        string name = args[1];
                        var _removeId = FromName(name);
                        if (_removeId is uint removeId)
                        {
                            Log.Info($"Utente da rimuovere: {removeId}");
                            if (removeId != ADMIN_ID)
                            {
                                await SendToClient(new ConnectionEnd(true, "Sei stato rimosso dalla partita"), removeId);
                                await SendToClients(new ChatMessage($"{name} è stato rimosso"));
                            }
                        }
                        else
                            await SendToClient(new ChatMessage($"L'utente {name} non esiste"), id);
                    }
                    else await SendToClient(new ChatMessage("Comando .remove non valido. Uso: .remove NomePlayer"), id);
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Gestisce le richieste di ogni client. Ogni client ha un suo thread apposito.
        /// </summary>
        /// <param name="client">Socket del client</param>
        /// <param name="userId">ID del client</param>
        /// <param name="channel">Canale di comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task ClientHandler(Socket client, uint userId, string name, CancellationToken canc)
        {
            Log.Info(client, "Avviato l'handler per il nuovo client.");

            // Salva l'ip in caso di disconnessione improvvisa
            string addr = Logger.ToAddress(client);
            while (true)
            {
                try
                {
                    canc.ThrowIfCancellationRequested();
                    // Riceve il tipo del pacchetto
                    short packetType = await Packet.ReceiveType(client, canc);
                    switch ((PacketType)packetType)
                    {
                        // Chiude la connessione
                        case PacketType.ConnectionEnd:
                            {
                                Log.Info(client, "Il client si vuole disconnettere");
                                var packet = await Packet.Receive<ConnectionEnd>(client);
                                if (packet.Final)
                                    goto abandon;
                                else
                                    goto close;
                            }
                        case PacketType.ChatMessage:
                            {
                                var packet = await Packet.Receive<ChatMessage>(client);
                                packet.FromId = userId;
                                var toSend = await ExecCommand(userId, packet.Message);
                                if (toSend)
                                    await SendToClients(packet);
                            }
                            continue;
                        default:
                            Log.Warn(client, $"Client ha mandato un packet type non valido: {packetType}");
                            await Packet.CancelReceive(client);
                            continue;
                    }
                }
                catch (PacketException e)
                {
                    Log.Error(addr, $"Packet exception durante l'handling di un client: {e}");
                    break;
                }
                catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
                {
                    Log.Info(addr, "Chiusura di questo client handler...");
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(addr, $"Errore durante l'handling di un client: {e}");
                    break;
                }
            }

        // Disconnessione possibilmente temporanea del client
        // I dati del giocatore vengono mantenuti in memoria.
        close:
            Log.Info(addr, "Disconnessione client...");
            await CloseClient(userId);

            // Manda l'update della disconnessione
            await UpdatePlayers();
            await SendToClients(new ChatMessage($"{name} si è disconnesso"));
            return;

        // Disconnessione permanente del client.
        // I dati del giocatore vengono rimossi dalla memoria.
        abandon:
            Log.Info(addr, "Rimozione client...");
            await RemovePlayer(userId);

            // Manda l'update della rimozione del player
            await UpdatePlayers();
            await SendToClients(new ChatMessage($"{name} ha abbandonato"));
            return;
        }

        /// <summary>
        /// Chiude la connessione di un giocatore.
        /// </summary>
        /// <param name="id">ID del giocatore</param>
        /// <returns></returns>
        private async Task CloseClient(uint id)
        {
            // Chiude la connessione
            ClientsMutex.WaitOne();
            if (Clients.TryGetValue(id, out var client))
                client.Close();
            Clients.Remove(id);
            ClientsMutex.ReleaseMutex();

            // Imposta il player come offline e cancella l'handler del client
            PlayersMutex.WaitOne();
            if (Players.TryGetValue(id, out var player))
            {
                player.Cancellation.Cancel();
                player.Player.IsOnline = false;
            }
            PlayersMutex.ReleaseMutex();

            await UpdatePlayers();
        }

        /// <summary>
        /// Rimuove un player
        /// </summary>
        /// <param name="id">ID del player da rimuovere</param>
        /// <returns></returns>
        private async Task RemovePlayer(uint id)
        {
            // Chiude la connessione
            ClientsMutex.WaitOne();
            if (Clients.TryGetValue(id, out var client))
                client.Close();
            Clients.Remove(id);
            ClientsMutex.ReleaseMutex();

            // Rimuove il giocatore
            PlayersMutex.WaitOne();
            if (Players.TryGetValue(id, out var player))
                player.Cancellation.Cancel();
            Players.Remove(id);
            PlayersMutex.ReleaseMutex();

            await UpdatePlayers();
        }

        /// <summary>
        /// Manda ai client la lista aggiornata di tutti i player
        /// </summary>
        /// <returns></returns>
        private async Task UpdatePlayers()
        {
            var players = new List<Player>();
            PlayersMutex.WaitOne();
            foreach (var player in Players)
                players.Add((Player)player.Value.Player.Clone());
            PlayersMutex.ReleaseMutex();
            var playersUpdate = new PlayersUpdate(players, null, null);
            await SendToClients(playersUpdate);
        }

        private async Task UpdatePlayer(uint id, bool isOnline) => await SendToClients(new PlayersUpdate(null, id, isOnline));

        /// <summary>
        /// Gestisce una nuova connessione.
        /// A seconda della richiesta aggiunge il nuovo client ai giocatori oppure
        /// effettua un rejoin, andando a segnare il giocatore nuovamente online.
        /// </summary>
        /// <param name="client">Nuova connessione</param>
        /// <param name="channel">Channel per la comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task NewConnectionHandler(Socket client)
        {
            Log.Info(client, "Avviato handler per la nuova connessione.");

            // Salva l'ip in caso di disconnessione improvvisa
            string addr = Logger.ToAddress(client);
            try
            {
                // Riceve il nome del pacchetto, se non è di tipo "Join" chiude la connessione
                short packetName = await Packet.ReceiveType(client, null);
                if (packetName != (short)PacketType.Join)
                {
                    Log.Warn(client, "Il client ha mandato un pacchetto non valido durante la connessione");
                    var status = new JoinStatus("Pacchetto non valido, una richiesta Join deve essere mandata");
                    await Packet.Send(client, status);
                    goto close;
                }

                // Riceve la richiesta e crea un nuovo handler per la nuova connessione, se la richiesta è valida
                var joinRequest = await Packet.Receive<Join>(client);
                switch (joinRequest.Type)
                {
                    case JoinType.Join:
                        if (!HasStarted)
                        {
                            // Nuovo ID del player
                            uint newID = (uint)Interlocked.Read(ref IdCount);
                            Interlocked.Increment(ref IdCount);

                            var cancSource = new CancellationTokenSource();

                            // Handler del player
                            var clientHandler = new Task(async () => await ClientHandler(client, newID, joinRequest.NewPlayer.Name, cancSource.Token));

                            // Creazione classe con i dati del giocatore
                            var playerData = new PlayerData(newID, clientHandler, joinRequest.NewPlayer, cancSource);

                            // Aggiunta player
                            PlayersMutex.WaitOne();
                            Players.Add(newID, playerData);
                            PlayersMutex.ReleaseMutex();

                            // Manda i nuovi dati generati dal server (ID e Access Code)
                            var status = new JoinStatus(playerData.Player, playerData.AccessCode);
                            await Packet.Send(client, status);

                            // Avvia il client handler
                            clientHandler.Start();

                            // Aggiunta connessione
                            ClientsMutex.WaitOne();
                            Clients.Add(newID, client);
                            ClientsMutex.ReleaseMutex();

                            // Manda la nuova lista dei player
                            await UpdatePlayers();
                            await SendToClients(new ChatMessage($"Un nuovo player si è unito: {playerData.Player.Name}"));

                            return;
                        }
                        else
                        {
                            Log.Warn(client, "Un nuovo client ha provato ad unirsi durante la partita");
                            var status = new JoinStatus("Il gioco è già iniziato");
                            await Packet.Send(client, status);
                            goto close;
                        }
                    case JoinType.Rejoin:
                        // Controlla che sia l'id che l'access code non siano null
                        if (joinRequest.Id is uint userId && joinRequest.AccessCode is long accessCode)
                        {
                            var cancSource = new CancellationTokenSource();
                            Task clientHandler;
                            long newAccessCode;
                            string name = "";
                            PlayersMutex.WaitOne();
                            if (Players.TryGetValue(userId, out var player))
                            {
                                // Aggiunge il socket nuovo del player, lo segna come online e genera il nuovo access code
                                // Se necessario cancella il client handler
                                if (player.AccessCode == accessCode && !player.Player.IsOnline)
                                {
                                    name = player.Player.Name;
                                    clientHandler = new Task(async () => await ClientHandler(client, userId, name, cancSource.Token));
                                    player.GenAccessCode();
                                    newAccessCode = player.AccessCode;
                                    if (player.ClientHandler != null)
                                    {
                                        if (!player.ClientHandler.IsCompleted)
                                            player.ClientHandler.Wait();
                                        player.ClientHandler.Dispose();
                                    }
                                    player.Cancellation = cancSource;
                                    player.ClientHandler = clientHandler;
                                    player.Player.IsOnline = true;
                                }
                                // Se l'access code non è valido o il player è ancora online la richiesta non è valida
                                else
                                {
                                    PlayersMutex.ReleaseMutex();
                                    goto invalid;
                                }
                            }
                            // Se l'utente non esiste la richiesta non è valida
                            else
                            {
                                PlayersMutex.ReleaseMutex();
                                goto invalid;
                            }
                            PlayersMutex.ReleaseMutex();

                            // Manda il nuovo access code
                            var rejoinStatusOk = new JoinStatus(newAccessCode);
                            await Packet.Send(client, rejoinStatusOk);

                            // Avvia il client handler
                            clientHandler.Start();

                            // Aggiunta connessione
                            ClientsMutex.WaitOne();
                            Clients.Add(userId, client);
                            ClientsMutex.ReleaseMutex();

                            // Manda la lista dei player al giocatore
                            await UpdatePlayers();
                            await SendToClients(new ChatMessage($"{name} si è riconnesso"));

                            return;
                        }
                    // Se l'id o l'access code non sono presenti la richiesta non è valida.
                    invalid:
                        Log.Warn(client, "Invalid rejoin request was sent");
                        var rejoinStatusErr = new JoinStatus("La richiesta per riunirsi al gioco non è valida.");
                        await Packet.Send(client, rejoinStatusErr);
                        goto close;
                    default:
                        Log.Warn(client, $"Invalid join request was sent: {joinRequest.Type}");
                        goto close;
                }
            }
            catch (PacketException e)
            {
                Log.Error(addr, $"Packet exception nell'handling di una nuova connessione: {e}");
            }
            catch (Exception e)
            {
                Log.Error(addr, $"Errore durante l'handling di una nuova connessione: {e}");
            }
        close:
            Log.Info(addr, "Disconnessione del client...");
            client.Close();
        }

        /// <summary>
        /// Inizializza il SocketServer e fa il binding dell'endpoint.
        /// </summary>
        private void InitSocket()
        {
            // Creazione socket del server
            IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(Address), Port);
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Binding e listening all'IP e alla porta specificati
            ServerSocket.Bind(ipEndpoint);
        }

        /// <summary>
        /// Task che ascolta le richieste in entrata e avvia NewConnectionHandler() per gestirle.
        /// </summary>
        /// <param name="channel">Canale di comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task Listener(CancellationToken canc)
        {
            ServerSocket.Listen(1000);
            Log.Info("Listener avviato.");
            while (true)
            {
                Socket client;
                try
                {
                    // Accetta nuove connessioni
                    Log.Info("Aspettando nuove connessioni...");
                    client = await ServerSocket.AcceptAsync(canc);
                    Log.Info(client, "Nuova connessione accettata");
                }
                catch (OperationCanceledException)
                {
                    Log.Info("Chiusura listener...");
                    return;
                }
                catch (Exception e)
                {
                    Log.Error($"Errore durante l'accept di una nuova connessione: {e}");
                    continue;
                }

                // Imposta il timeout
                client.ReceiveTimeout = -1;
                client.SendTimeout = TimeOutMillis;

                // Avvia Task per l'accettazione del client
                Log.Info(client, "Avvio handler...");
                new Task(async () => await NewConnectionHandler(client)).Start();
            }
        }
    }
}
