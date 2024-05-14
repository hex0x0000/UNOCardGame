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
        private readonly IPAddress Address;

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
                var random = new Random();
                byte[] buffer = new byte[sizeof(ulong)];
                random.NextBytes(buffer);
                AccessCode = BitConverter.ToUInt64(buffer, 0);
                Deck = Card.GenerateDeck(7);
                Client = client;
                ClientHandler = clientHandler;
                Player = new Player(id, player.Name, player.Personalizations);
                IsOnline = true;
            }

            /// <summary>
            /// Il codice di accesso è necessario per evitare impersonificazioni.
            /// E' necessario anche in caso di riconnessione.
            /// </summary>
            public ulong AccessCode { get; }

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
            public Player Player { get; }

            /// <summary>
            /// Il deck del giocatore.
            /// </summary>
            public List<Card> Deck { get; }

            /// <summary>
            /// Segna se il giocatore è online o meno.
            /// </summary>
            public bool IsOnline { get; set; }
        }

        private struct ChannelData
        {
            public ChannelData(short id, object data)
            {
                PacketId = id; Data = data;
            }

            public readonly short PacketId;
            public readonly object Data;
        }

        public Server(string address, ushort port)
        {
            Address = IPAddress.Parse(address);
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
        private async Task BroadcastAll<T>(T packet) where T : Serialization<T>, ICloneable
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
                            return;
                        default:
                            if (packet.PacketId == ChatMessage.GetPacketId())
                                // Un messaggio della chat può essere mandato a tutti i client
                                await BroadcastAll((ChatMessage)packet.Data);
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
                        // Chiude la connessione o rimuove il player a seconda della richiesta
                        // del player.
                        case Packet.ConnectionEnd:
                            goto close;
                        case Packet.ClientEnd:
                            goto abandon;
                        default:
                            if (packetType == ChatMessage.GetPacketId())
                            {
                                var packet = await Packet.Receive<ChatMessage>(client);
                                packet.FromId = userId;
                                await channel.WriteAsync(new ChannelData(packetType, packet.Clone()));
                            }
                            else
                                await Packet.CancelReceive(client);
                            break;
                    }
                    continue;
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
                PlayersMutex.WaitOne();
                if (Players.TryGetValue(userId, out var player))
                {
                    player.IsOnline = false;
                    player.Client = null;
                }
                PlayersMutex.ReleaseMutex();
                client.Close();
                return;

            // Disconnessione permanente del client.
            // I dati del giocatore vengono rimossi dalla memoria.
            abandon:
                Log.Info(client, "Removing client...");
                PlayersMutex.WaitOne();
                Players.Remove(userId);
                PlayersMutex.ReleaseMutex();
                client.Close();
                return;
            }
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
                if (packetName != Join.GetPacketId())
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
                            var status = new JoinStatus(new NewPlayerData(playerData.Player, playerData.AccessCode));
                            await Packet.Send(client, status);

                            // Avvia handler
                            clientHandler.Start();
                        }
                        else
                        {
                            Log.Warn(client, "New client tried to connect while playing");
                            var status = new JoinStatus("Il gioco è già iniziato");
                            await Packet.Send(client, status);
                            goto close;
                        }
                        return;
                    case JoinType.Rejoin:
                        // Controlla che sia l'id che l'access code non siano null
                        if (joinRequest.Id is uint userId && joinRequest.AccessCode is ulong accessCode)
                        {
                            var clientHandler = new Task(async () => await ClientHandler(client, userId, channel));
                            PlayersMutex.WaitOne();
                            if (Players.TryGetValue(userId, out var player))
                            {
                                // Aggiunge il socket nuovo del player e lo segna come online
                                if (player.AccessCode == accessCode)
                                {
                                    player.ClientHandler = clientHandler;
                                    player.Client = client;
                                    player.IsOnline = true;
                                }
                                // Se l'access code non è valido la richiesta non è valida
                                else goto release;
                            }
                            // Se l'utente non esiste la richiesta non è valida
                            else goto release;
                            PlayersMutex.ReleaseMutex();
                            clientHandler.Start();
                            return;
                        }
                        // Se l'id o l'access code non sono presenti la richiesta non è valida,
                        // ma si salta il ReleaseMutex() perché non è stato bloccato
                        else goto invalid;

                        release:
                        PlayersMutex.ReleaseMutex();
                    invalid:
                        Log.Warn(client, "Invalid packet was sent");
                        var rejoinStatus = new JoinStatus("La richiesta di riunirsi al gioco non è valida.");
                        await Packet.Send(client, rejoinStatus);
                        goto close;
                    default:
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
        /// Task che ascolta le richieste in entrata e avvia NewConnectionHandler() per gestirle.
        /// </summary>
        /// <param name="channel">Canale di comunicazione con il broadcaster</param>
        /// <returns></returns>
        private async Task Listener(ChannelWriter<ChannelData> channel)
        {
            // Creazione socket del server
            IPEndPoint ipEndpoint = new IPEndPoint(Address, Port);
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Binding e listening all'IP e alla porta specificati
            ServerSocket.Bind(ipEndpoint);
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
