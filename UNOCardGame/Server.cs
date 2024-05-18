using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using UNOCardGame.Packets;

namespace UNOCardGame
{
    class Server
    {
        /// <summary>
        /// Timeout della connessione.
        /// </summary>
        private const int TimeOutMillis = 20 * 1000;

        /// <summary>
        /// Generatore di numeri casuali.
        /// </summary>
        private static Random _Random = new Random();

        private int _RunFlag = 0;

        /// <summary>
        /// Gli handler continuano a funzionare finché questa proprietà non viene messa a false.
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool RunFlag
        {
            get => (Interlocked.CompareExchange(ref _RunFlag, 1, 1) == 0); set
            {
                if (value) Interlocked.CompareExchange(ref _RunFlag, 1, 0);
                else Interlocked.CompareExchange(ref _RunFlag, 0, 1);
            }
        }

        private int _HasStarted = 0;

        /// <summary>
        /// Indica se il gioco è iniziato o meno.
        /// Questa proprietà può essere modificata in modo thread-safe.
        /// </summary>
        private bool HasStarted
        {
            get => (Interlocked.CompareExchange(ref _HasStarted, 1, 1) == 0); set
            {
                if (value) Interlocked.CompareExchange(ref _HasStarted, 1, 0);
                else Interlocked.CompareExchange(ref _HasStarted, 0, 1);
            }
        }

        /// <summary>
        /// Tiene conto del numero degli user id.
        /// Il numero degli ID deve essere ordinato e univoco per mantenere l'ordine dei turni.
        /// Non thread-safe, Interlocked deve essere usato per modificare la variabile.
        /// </summary>
        private long IdCount = 0;

        /// <summary>
        /// Indirizzo IP su cui ascolta il server.
        /// </summary>
        private readonly string Address;

        /// <summary>
        /// Porta su cui ascolta il server.
        /// </summary>
        private readonly ushort Port;

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
        /// Canale di comunicazione per gli handler.
        /// </summary>
        private Channel<ChannelData> Communicator;

        /// <summary>
        /// Tutti i player del gioco. Questo dictionary contiene tutti i dati necessari per comunicare con i client.
        /// E' necessario accedervi con il mutex bloccato, dato che questo oggetto non è thread-safe.
        /// </summary>
        private Dictionary<uint, PlayerData> Players = new Dictionary<uint, PlayerData>();

        /// <summary>
        /// Mutex che coordina l'accesso a Players
        /// </summary>
        private static Mutex PlayersMutex = new Mutex();

        /// <summary>
        /// Socket del server. Usato per accettare le nuove connessioni.
        /// </summary>
        private Socket ServerSocket;

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
            public PlayerData(uint id, Socket client, Task clientHandler, Player player)
            {
                GenAccessCode();
                Deck = Card.GenerateDeck(7);
                Client = client;
                ClientHandler = clientHandler;
                IsOnline = true;
                Player = new Player(id, Deck.Count, IsOnline, player.Name, player.Personalizations);
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
            /// Socket della connessione al client. 
            /// </summary>
            public Socket Client { get; set; }

            /// <summary>
            /// Task dell'handler del client.
            /// </summary>
            public Task ClientHandler { get; set; }

            /// <summary>
            /// Dati del player non legati alla connessione.
            /// </summary>
            public Player Player { get; set; }

            /// <summary>
            /// Il deck del giocatore.
            /// </summary>
            public List<Card> Deck { get; set; }

            /// <summary>
            /// Segna se il giocatore è online o meno.
            /// </summary>
            public bool IsOnline { get; set; }
        }

        /// <summary>
        /// Dati mandati tramite il Communicator
        /// </summary>
        private struct ChannelData
        {
            public ChannelData(short id, object data)
            {
                PacketId = id; Data = data; SendTo = null;
            }

            public ChannelData(short id, object data, uint sendTo)
            {
                PacketId = id; Data = data; SendTo = sendTo;
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
            public readonly uint? SendTo;
        }

        public Server(string address, ushort port)
        {
            Address = address;
            Port = port;
        }

        ~Server()
        {
            StopServer();
            ListenerHandler.Dispose();
            BroadcasterHandler.Dispose();
            // TODO: Gamemaster
            foreach (var player in Players)
                player.Value.ClientHandler.Dispose();
            PlayersMutex.Dispose();
        }

        public void StartServer()
        {
            InitSocket();
            Communicator = Channel.CreateUnbounded<ChannelData>();
            RunFlag = true;

            // TODO: Gamemaster

            // Broadcaster
            BroadcasterHandler = new Task(async () => await Broadcaster(Communicator.Reader));
            BroadcasterHandler.Start();

            // Listener
            ListenerHandler = new Task(async () => await Listener(Communicator.Writer));
            ListenerHandler.Start();
        }

        public void StopServer()
        {
            // Tempo di timeout
            const int timeOut = 5000;

            // Interrompe i cicli
            RunFlag = false;

            // Causa la chiusura del listener
            ServerSocket.Close();

            // Causa la chiusura del broadcaster
            Communicator.Writer.TryWrite(new ChannelData(Packet.ServerEnd, null));
            Communicator.Writer.Complete();

            Log.Info("Stopping listener...");
            if (!ListenerHandler.IsCompleted)
                ListenerHandler.Wait(timeOut);

            Log.Info("Stopping broadcaster...");
            if (!BroadcasterHandler.IsCompleted)
                BroadcasterHandler.Wait(timeOut);

            // TODO: Gamemaster

            // Termina le connessioni con i player e i loro handler
            foreach (var player in Players)
            {
                Log.Info($"Terminating player '{player.Value.Player.Name}'...");
                if (!player.Value.ClientHandler.IsCompleted)
                    player.Value.ClientHandler.Wait(timeOut);
                try
                {
                    if (player.Value.IsOnline)
                        player.Value.Client.Close();
                }
                catch (Exception e)
                {
                    Log.Error($"Failed to close client: {e}");
                }
            }
        }

        /// <summary>
        /// Manda un pacchetto a tutti i client.
        /// </summary>
        /// <typeparam name="T">Tipo serializzabile</typeparam>
        /// <param name="packet">Un pacchetto qualsiasi</param>
        /// <returns></returns>
        private async Task BroadcastAll<T>(T packet) where T : Serialization<T>
        {
            var clients = new List<Socket>();

            // Copia i socket dei client per evitare di passare troppo tempo con il mutex bloccato
            PlayersMutex.WaitOne();
            foreach (var player in Players)
                if (player.Value.IsOnline)
                    clients.Add(player.Value.Client);
            PlayersMutex.ReleaseMutex();

            // Manda il messaggio a tutti i socket
            foreach (var client in clients)
                await Packet.Send(client, packet);
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
            Socket client;

            // Prende il socket dalla lista dei player
            PlayersMutex.WaitOne();
            if (Players.TryGetValue(id, out var player))
                client = player.Client;
            else client = null;
            PlayersMutex.ReleaseMutex();

            // Manda il messaggio a tutti al socket se esiste, altrimenti viene ignorato
            if (client != null)
                await Packet.Send(client, packet);
        }

        /// <summary>
        /// Thread che manda i pacchetti ai client.
        /// A seconda della richiesta del Canale può mandarli a un utente specifico o a tutti.
        /// </summary>
        private async Task Broadcaster(ChannelReader<ChannelData> channel)
        {
            while (RunFlag)
            {
                try
                {
                    // Aspetta che gli vengano mandati nuovi pacchetti da mandare
                    var packet = await channel.ReadAsync();
                    switch (packet.PacketId)
                    {
                        case Packet.ServerEnd:
                            // TODO: Broadcasting della chiusura del server
                            return;
                        case (short)PacketType.ChatMessage:
                            {
                                if (packet.SendTo is uint sendTo)
                                    await BroadcastTo(sendTo, (ChatMessage)packet.Data);
                                else
                                    await BroadcastAll((ChatMessage)packet.Data);
                            }
                             break;
                        case (short)PacketType.PlayerUpdate:
                            {
                                if (packet.SendTo is uint sendTo)
                                    await BroadcastTo(sendTo, (PlayerUpdate)packet.Data);
                                else
                                    await BroadcastAll((PlayerUpdate)packet.Data);
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch (PacketException e)
                {
                    Log.Error($"Packet exception occurred while broadcasting packet: {e}");
                }
                catch (Exception e)
                {
                    Log.Error($"Exception occurred while broadcasting packet: {e}");
                }
            }
        }

        /// <summary>
        /// Gestisce le richieste di ogni client. Ogni client ha un suo thread apposito.
        /// </summary>
        /// <param name="client">Socket del client</param>
        /// <param name="userId">ID del client</param>
        /// <param name="channel">Canale di comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task ClientHandler(Socket client, uint userId, ChannelWriter<ChannelData> channel)
        {
            while (RunFlag)
            {
                try
                {
                    // Riceve il tipo del pacchetto
                    short packetType = await Packet.ReceiveType(client);
                    switch (packetType)
                    {
                        // Chiude la connessione
                        case Packet.ConnectionEnd:
                            goto close;
                        // Rimuove il player
                        case Packet.ClientEnd:
                            goto abandon;
                        case (short)PacketType.ChatMessage:
                            {
                                var packet = await Packet.Receive<ChatMessage>(client);
                                packet.FromId = userId;
                                await channel.WriteAsync(new ChannelData(packetType, packet));
                            }
                            continue;
                        default:
                            Log.Warn(client, $"Client sent an invalid packet type: {packetType}");
                            await Packet.CancelReceive(client);
                            continue;
                    }
                }
                catch (PacketException e)
                {
                    Log.Error(client, $"Packet exception happened while handling client: {e}");
                }
                catch (Exception e)
                {
                    Log.Error(client, $"Exception happened while handling client: {e}");
                }

            // Disconnessione possibilmente temporanea del client
            // I dati del giocatore vengono mantenuti in memoria.
            close:
                Log.Info(client, "Disconnecting client...");
                
                // Chiude la connessione
                client.Close();

                // Imposta il client offline
                PlayersMutex.WaitOne();
                if (Players.TryGetValue(userId, out var player))
                {
                    player.IsOnline = false;
                    player.Client = null;
                }
                PlayersMutex.ReleaseMutex();

                // Manda l'update della disconnessione
                var playerDisconnect = new PlayerUpdate(userId, false);
                await channel.WriteAsync(new ChannelData(playerDisconnect.PacketId, playerDisconnect));

                return;

            // Disconnessione permanente del client.
            // I dati del giocatore vengono rimossi dalla memoria.
            abandon:
                Log.Info(client, "Removing client...");
                
                // Chiude la connessione
                client.Close();

                // Rimuove il client
                PlayersMutex.WaitOne();
                Players.Remove(userId);
                PlayersMutex.ReleaseMutex();

                // Manda l'update della rimozione del player
                var playerRemoved = new PlayerUpdate(userId);
                await channel.WriteAsync(new ChannelData(playerRemoved.PacketId, playerRemoved));
                
                return;
            }
        }

        /// <summary>
        /// Ritorna la lista con tutti i giocatori nel server.
        /// </summary>
        /// <returns></returns>
        private List<Player> GetPlayersList()
        {
            var players = new List<Player>();
            PlayersMutex.WaitOne();
            foreach (var player in Players)
                players.Add((Player)player.Value.Player.Clone());
            PlayersMutex.ReleaseMutex();
            return players;
        }

        /// <summary>
        /// Gestisce una nuova connessione.
        /// A seconda della richiesta aggiunge il nuovo client ai giocatori oppure
        /// effettua un rejoin, andando a segnare il giocatore nuovamente online.
        /// </summary>
        /// <param name="client">Nuova connessione</param>
        /// <param name="channel">Channel per la comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task NewConnectionHandler(Socket client, ChannelWriter<ChannelData> channel)
        {
            try
            {
                // Riceve il nome del pacchetto, se non è di tipo "Join" chiude la connessione
                short packetName = await Packet.ReceiveType(client);
                if (packetName != (short)PacketType.Join)
                {
                    Log.Warn(client, "Client sent invalid packet while joining");
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

                            // Handler del player
                            var clientHandler = new Task(async () => await ClientHandler(client, newID, channel));

                            // Creazione struct con i dati del giocatore
                            var playerData = new PlayerData(newID, client, clientHandler, joinRequest.NewPlayer);

                            // Aggiunta player
                            PlayersMutex.WaitOne();
                            Players.Add(newID, playerData);
                            PlayersMutex.ReleaseMutex();

                            // Manda i nuovi dati generati dal server (ID e Access Code)
                            var status = new JoinStatus(playerData.Player, playerData.AccessCode);
                            await Packet.Send(client, status);

                            // Avvia il client handler
                            clientHandler.Start();

                            // Manda la lista dei player al nuovo giocatore
                            var playersList = new PlayerUpdate(GetPlayersList());
                            await channel.WriteAsync(new ChannelData(playersList.PacketId, playersList, newID));

                            // Manda il nuovo giocatore a tutti i player
                            var newPlayer = new PlayerUpdate(playerData.Player);
                            await channel.WriteAsync(new ChannelData(newPlayer.PacketId, newPlayer));

                            return;
                        }
                        else
                        {
                            Log.Warn(client, "New client tried to connect while playing");
                            var status = new JoinStatus("Il gioco è già iniziato");
                            await Packet.Send(client, status);
                            goto close;
                        }
                    case JoinType.Rejoin:
                        // Controlla che sia l'id che l'access code non siano null
                        if (joinRequest.Id is uint userId && joinRequest.AccessCode is long accessCode)
                        {
                            var clientHandler = new Task(async () => await ClientHandler(client, userId, channel));
                            long newAccessCode;
                            PlayersMutex.WaitOne();
                            if (Players.TryGetValue(userId, out var player))
                            {
                                // Aggiunge il socket nuovo del player e lo segna come online
                                if (player.AccessCode == accessCode && !player.IsOnline)
                                {
                                    player.GenAccessCode();
                                    newAccessCode = player.AccessCode;
                                    if (!player.ClientHandler.IsCompleted)
                                        player.ClientHandler.Wait(1000);
                                    player.ClientHandler.Dispose();
                                    player.ClientHandler = clientHandler;
                                    player.Client = client;
                                    player.IsOnline = true;
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

                            // Manda la lista dei player al giocatore
                            var playersList = new PlayerUpdate(GetPlayersList());
                            await channel.WriteAsync(new ChannelData(playersList.PacketId, playersList, userId));

                            // Manda l'update dello status del player a tutti i giocatori
                            var newPlayer = new PlayerUpdate(userId, true);
                            await channel.WriteAsync(new ChannelData(newPlayer.PacketId, newPlayer));

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
                Log.Error(client, $"Packet exception occurred while handling new connection: {e}");
            }
            catch (Exception e)
            {
                Log.Error(client, $"Exception occurred while handling a new connection: {e}");
            }
        close:
            Log.Info(client, "Disconnecting...");
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
        private async Task Listener(ChannelWriter<ChannelData> channel)
        {
            ServerSocket.Listen(1000);
            while (RunFlag)
            {
                Socket client;
                try
                {
                    // Accetta nuove connessioni
                    client = await ServerSocket.AcceptAsync();
                }
                catch (ObjectDisposedException)
                {
                    // Se il socket del server è stato chiuso termina la funzione
                    return;
                }
                catch (Exception e)
                {
                    Log.Error($"An exception occurred while listening for new connections: {e}");
                    continue;
                }

                // Imposta il timeout
                client.ReceiveTimeout = -1;
                client.SendTimeout = TimeOutMillis;

                // Avvia Task per l'accettazione del client
                Log.Info(client, "New connection");
                new Task(async () => await NewConnectionHandler(client, channel)).Start();
            }
        }
    }
}
