using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
        private const int _TimeOutMillis = 50 * 1000;

        /// <summary>
        /// Il server continua ad aspettare connessioni finché questa flag non viene messa a false.
        /// </summary>
        private bool runFlag = true;

        private bool hasStarted = false;

        private bool turnId;

        /// <summary>
        /// Il socket del server.
        /// Viene usato per la comunicazione con i client.
        /// </summary>
        private Socket server;

        /// <summary>
        /// Tiene conto del numero degli id.
        /// Il numero degli ID deve essere ordinato per mantenere l'ordine dei turni.
        /// </summary>
        private uint idCount = 0;

        /// <summary>
        /// Tutti i player del gioco, a parte l'host.
        /// Questo hashmap contiene tutti i dati necessari per comunicare con i client.
        /// </summary>
        private Dictionary<uint, PlayerData> players = new Dictionary<uint, PlayerData>();

        /// <summary>
        /// I dati di ogni player.
        /// Contiene il codice di accesso, il server per comunicare con il client
        /// e le informazioni del player.
        /// </summary>
        private struct PlayerData
        {
            /// <summary>
            /// Constructor di PlayerData. Genera automaticamente l'access code.
            /// </summary>
            /// <param name="id"></param>
            /// <param name="client"></param>
            /// <param name="clientHandler"></param>
            /// <param name="player"></param>
            public PlayerData(uint id, Socket client, Thread clientHandler, Player player)
            {
                var random = new RNGCryptoServiceProvider();
                byte[] buffer = new byte[sizeof(ulong)];
                random.GetBytes(buffer);
                AccessCode = BitConverter.ToUInt64(buffer, 0);
                Deck = Card.GenerateDeck(7);
                Client = client;
                ClientHandler = clientHandler;
                Player = new Player(id, player.Name, player.Personalizations);
            }

            /// <summary>
            /// Il codice di accesso è necessario per evitare impersonificazioni.
            /// E' necessario anche in caso di riconnessione.
            /// </summary>
            public ulong AccessCode { get; }

            /// <summary>
            /// Socket della connessione al client. 
            /// </summary>
            public Socket Client { get; }

            /// <summary>
            /// Thread dell'handler del client.
            /// </summary>
            public Thread ClientHandler { get; }

            /// <summary>
            /// Dati del player non legati alla connessione.
            /// </summary>
            public Player Player { get; }

            /// <summary>
            /// Il deck del giocatore.
            /// </summary>
            public List<Card> Deck { get; }
        }

        public Server(string address, short port)
        {
            IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(address), port);
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(ipEndpoint);
        }

        /// <summary>
        /// Thread che manda i pacchetti ai client.
        /// A seconda della richiesta del Canale può mandarli a un utente specifico o a tutti.
        /// </summary>
        private void BroadCaster() { }

        /// <summary>
        /// Gestisce le richieste di ogni client. Ogni client ha un suo thread apposito.
        /// </summary>
        /// <param name="client">Il client dato come parametro durantre la creazione del nuovo thread</param>
        private void ClientHandler(Socket client)
        {
            while (true)
            {
                try
                {
                    string packetName = Packet.ReceiveName(client);
                    switch (packetName)
                    {
                        case nameof(ChatMessage):
                            break;
                        case "Disconnect":
                            goto close;
                        default:
                            Packet.CancelReceive(client);
                            var status = new Status<object, string>("Nome del pacchetto non valido");
                            // TODO: implementare broadcast
                            //lock (client)
                            //    Packet.Send(client, status);
                            break;
                    }
                    continue;
                }
                catch (PacketException e)
                {
                    Log.Error(client, $"Packet exception happened while handling client: {e}");
                    switch (e.ExceptionType)
                    {
                        case PacketExceptions.SocketFailed:
                            goto close;
                        default:
                            continue;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(client, $"Exception happened while handling client: {e}");
                    goto close;
                }
            close:
                Log.Info(client, "Disconnecting client...");
                client.Close();
                return;
            }
        }

        /// <summary>
        /// Thread che ascolta le richieste in entrata e le gestisce.
        /// </summary>
        public void Listen()
        {
            server.Listen(1000);
            while (runFlag)
            {
                // Accetta nuove connessioni 
                var client = server.Accept();
                client.ReceiveTimeout = _TimeOutMillis;
                client.SendTimeout = _TimeOutMillis;
                Log.Info(client, "New connection");

                try
                {
                    // Riceve il nome del pacchetto, se non è di tipo "Join" chiude la connessione
                    string packetName = Packet.ReceiveName(client);
                    if (packetName != nameof(Join))
                    {
                        Log.Warn(client, "Client sent invalid packet while joining");
                        var status = new Status<Player, string>("Pacchetto non valido, una richiesta Join deve essere mandata");
                        Packet.Send(client, status);
                        goto close;
                    }

                    // Riceve la richiesta e crea nuovi handler per ogni nuova connessione
                    var joinRequest = Packet.Receive<Join>(client);
                    switch (joinRequest.Type)
                    {
                        case JoinType.Join:
                            if (!hasStarted)
                            {
                                // Nuovo ID del player
                                uint newID = idCount;
                                idCount++;

                                // Handler del player
                                var clientHandler = new Thread(() => ClientHandler(client))
                                {
                                    Name = client.ToString()
                                };

                                // Creazione struct con i dati del giocatore
                                var playerData = new PlayerData(newID, client, clientHandler, joinRequest.NewPlayer);

                                // Aggiunta player 
                                players.Add(newID, playerData);

                                // Manda i nuovi dati generati dal server (ID e Access Code)
                                var status = new Status<NewPlayerData, string>(new NewPlayerData(playerData.Player, playerData.AccessCode));
                                Packet.Send(client, status);

                                // Avvia handler
                                clientHandler.Start();
                            }
                            else
                            {
                                Log.Warn(client, "New client tried to connect while playing");
                                var status = new Status<NewPlayerData, string>("Il gioco è già iniziato");
                                Packet.Send(client, status);
                                goto close;
                            }
                            continue;
                        case JoinType.Rejoin:
                            // TODO: implementare rejoin.
                            continue;
                        default:
                            goto close;
                    }
                }
                catch (PacketException e)
                {
                    Log.Error(client, $"Packet exception occurred while handling new connection: {e}");
                    goto close;
                }
                catch (Exception e)
                {
                    Log.Error(client, $"Exception occurred while handling a new connection: {e}");
                    goto close;
                }
            close:
                Log.Info(client, "Disconnecting...");
                client.Close();
                continue;
            }
        }
    }
}
