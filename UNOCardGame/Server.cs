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
    class Server(string address, ushort port, ServerOptions options)
    {
        /// <summary>
        /// ID dell'admin. Sempre 0
        /// </summary>
        public const int ADMIN_ID = 0;

        /// <summary>
        /// Numero massimo di player che possono unirsi al Server.
        /// </summary>
        private readonly int MaxPlayers = options.MaxPlayers;

        /// <summary>
        /// Messaggio di entrata nel server
        /// </summary>
        private readonly ChatMessage MotdMessage = new(options.MotdMessage);

        /// <summary>
        /// Timeout della connessione.
        /// </summary>
        private const int TimeOutMillis = 20 * 1000;

        /// <summary>
        /// Generatore di numeri casuali.
        /// </summary>
        private static readonly Random _Random = new();

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
        public readonly string Address = address;

        /// <summary>
        /// Porta su cui ascolta il server.
        /// </summary>
        public readonly ushort Port = port;

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
        private readonly Dictionary<uint, PlayerData> Players = [];

        /// <summary>
        /// Mutex che coordina l'accesso a Players
        /// </summary>
        private static readonly SemaphoreSlim PlayersLock = new(1, 1);

        /// <summary>
        /// I socket dei giocatori.
        /// Risiedono su un hashmap diverso per evitare dei bottleneck durante la spedizione dei pacchetti.
        /// </summary>
        private readonly Dictionary<uint, Socket> Clients = [];

        /// <summary>
        /// Mutex che coordina l'accesso ai client.
        /// </summary>
        private static readonly SemaphoreSlim ClientsLock = new(1, 1);

        /// <summary>
        /// Socket del server. Usato per accettare le nuove connessioni.
        /// </summary>
        private Socket ServerSocket;

        /// <summary>
        /// Logger del server
        /// </summary>
        private readonly Logger Log = new("SERVER");

        /// <summary>
        /// Ferma il listener
        /// </summary>
        private CancellationTokenSource ListenerCancellation;

        /// <summary>
        /// Ferma il broadcaster
        /// </summary>
        private CancellationTokenSource BroadcasterCancellation;

        /// <summary>
        /// Ferma il gamemaster.
        /// Se il gamemaster è in una partita un nuovo gamemaster viene lanciato per aspettare l'avvio,
        /// altrimenti viene fermato definitivamente
        /// </summary>
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
                ClientHandler = clientHandler;
                Player = new Player(id, true, player.Name, player.Personalizations, null);
                Cancellation = canc;
            }

            /// <summary>
            /// Crea il playerdata dell'host.
            /// </summary>
            /// <param name="player">Informazioni del player dell'host</param>
            public PlayerData(Player player)
            {
                GenAccessCode();
                Player = new Player(0, false, player.Name, player.Personalizations, null);
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

            /// <summary>
            /// Determina se è necessario chiudere la connessione o rimuovere il player
            /// quando viene mandato il cancellation token
            /// </summary>
            public bool Remove = false;
        }

        /// <summary>
        /// Dati mandati tramite il Communicator
        /// </summary>
        private readonly struct ChannelData
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

        /// <summary>
        /// PacketId dell'InternalComm
        /// </summary>
        private const short INTERNALCOMM_ID = -1;

        /// <summary>
        /// Comunicazioni interne al server
        /// </summary>
        private enum InternalComm
        {
            /// <summary>
            /// Salta il turno del player, in caso di disconnesioni
            /// </summary>
            SkipPlayer,

            /// <summary>
            /// Aggiorna lo stato del player in caso di riconnessione
            /// </summary>
            UpdateCardsOfPlayer,

            /// <summary>
            /// Comunica che il player ha detto UNO!
            /// </summary>
            SaidUno
        }

        ~Server()
        {
            Stop();
            ListenerHandler.Dispose();
            BroadcasterHandler.Dispose();
            GameMasterHandler.Dispose();
            foreach (var player in Players)
                player.Value.ClientHandler.Dispose();
            PlayersLock.Dispose();
            ClientsLock.Dispose();
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
            GameMasterCommunicator = Channel.CreateUnbounded<ChannelData>();

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

        /// <summary>
        /// Interrompe il server
        /// </summary>
        public void Stop()
        {
            // Tempo di timeout
            const int timeOut = 1000;

            // Chiude il server solo se è stato inizializzato prima
            if (ServerSocket != null && ListenerCancellation != null)
            {
                var endTask = new Task(async () => await SendToClients(new ConnectionEnd(true, "Il server è stato chiuso.")));
                endTask.Start();
                endTask.Wait();

                // Aspetta che il broadcaster finisca di mandare tutti i messaggi
                while (IsBroadcasterRunning) ;

                ListenerCancellation.Cancel();
                Log.Info("Aspettando la chiusura del listener...");
                if (!ListenerHandler.IsCompleted)
                    ListenerHandler.Wait(timeOut);

                BroadcasterCancellation.Cancel();
                Log.Info("Aspettando la chiusura del broadcaster...");
                if (!BroadcasterHandler.IsCompleted)
                    BroadcasterHandler.Wait(timeOut);

                GameMasterCancellation.Cancel();
                Log.Info("Aspettando la chiusura del gamemaster...");
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

        /// <summary>
        /// Prende il nome del player e il suo deck partendo dall'id
        /// </summary>
        /// <param name="id">ID del player</param>
        /// <returns></returns>
        private async Task<(Deck, string)> GetPlayerDeckAndName(uint id)
        {
            Deck deck = null;
            string name = null;
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var playerData))
            {
                deck = playerData.Deck;
                name = playerData.Player.Name;
            }
            PlayersLock.Release();
            return (deck, name);
        }

        /// <summary>
        /// Imposta il deck del player
        /// </summary>
        /// <param name="id"></param>
        /// <param name="deck"></param>
        /// <returns></returns>
        private async Task SetPlayerDeck(uint id, Deck deck)
        {
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var player))
                player.Deck = deck;
            PlayersLock.Release();
        }

        /// <summary>
        /// Ritorna l'ID dell'utente del prossimo turno
        /// </summary>
        /// <param name="prevId">ID dell'utente del turno prima</param>
        /// <param name="isLeftToRight">Ordine del turno</param>
        /// <returns></returns>
        private async Task<uint> NextPlayer(uint prevId, bool isLeftToRight)
        {
            uint nextId = 0;
            await PlayersLock.WaitAsync();
            var playersAvailable = Players.Where(data => data.Value.Player.IsOnline && data.Value.Player.Won == null).ToDictionary().Keys.ToList();
            PlayersLock.Release();
            if (playersAvailable.Count < 2)
            {
                GameMasterCancellation.Cancel();
                return 0;
            }
            playersAvailable.Sort();
            if (isLeftToRight)
            {
                // Cerca di trovare il giocatore online dopo il giocatore di questo turno
                int indexNext = 0;
                uint i = 1;
                while (true)
                {
                    // Se l'iterazione supera l'ID più grande nella lista riparte da 0
                    if (prevId + i > playersAvailable.Last())
                    { indexNext = 0; break; }

                    // Indice dell'utente del prossimo turno
                    indexNext = playersAvailable.FindIndex(0, playersAvailable.Count, (prevId + i).Equals);

                    // Se l'ID non esiste riprova con l'ID dopo
                    if (indexNext == -1)
                    { i++; continue; }

                    break;
                }
                nextId = playersAvailable[indexNext];
            }
            else
            {
                // Cerca di trovare il giocatore online dopo il giocatore di questo turno, andando da destra a sinistra
                int indexNext = 0;
                int i = 1;
                while (true)
                {
                    if ((int)prevId - i < 0)
                    { indexNext = playersAvailable.Count - 1; break; }

                    // Indice dell'utente del prossimo turno
                    indexNext = playersAvailable.FindIndex(0, playersAvailable.Count, ((uint)(prevId - i)).Equals);

                    // Se l'ID non esiste riprova con l'ID dopo
                    if (indexNext == -1)
                    { i++; continue; }

                    break;
                }
                nextId = playersAvailable[indexNext];
            }
            return nextId;
        }

        /// <summary>
        /// Prende il numero di carte di tutti i player
        /// </summary>
        /// <returns></returns>
        private async Task<Dictionary<uint, int>> GetPlayersCardsNum()
        {
            Dictionary<uint, int> playersCardsNum = [];
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
                playersCardsNum.Add(player.Key, player.Value.Deck.Cards.Count);
            PlayersLock.Release();
            return playersCardsNum;
        }

        /// <summary>
        /// Resetta le info degli utenti allo stato iniziale del gioco
        /// </summary>
        /// <returns></returns>
        private async Task ResetGame()
        {
            Dictionary<uint, Deck> decks = [];
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
            {
                player.Value.Player.Won = null;
                player.Value.Deck = new Deck();
                decks.Add(player.Key, player.Value.Deck);
            }
            PlayersLock.Release();
            foreach (var deck in decks)
                await SendToClient(new TurnUpdate(deck.Value.Cards), deck.Key);
        }

        /// <summary>
        /// Aggiunge carte a un player
        /// </summary>
        /// <param name="addCards">Numero di carte da aggiungere</param>
        /// <param name="playerId">ID del player</param>
        /// <param name="isOnline">Indica se il player a cui si aggiungono carte è online o no</param>
        /// <returns></returns>
        private async Task AddCardsToPlayer(uint addCards, uint playerId)
        {
            if (addCards == 0) addCards = 1;
            var (deck, _) = await GetPlayerDeckAndName(playerId);
            if (deck != null)
            {
                deck.Add(addCards);
                await SetPlayerDeck(playerId, deck);
            }
        }

        /// <summary>
        /// Fa vincere un player
        /// </summary>
        /// <param name="id">ID del player</param>
        /// <param name="wonCount">Conteggio in classifica</param>
        /// <returns></returns>
        private async Task PlayerWon(uint id, int wonCount)
        {
            string name = null;
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var playerData))
            {
                name = playerData.Player.Name;
                playerData.Player.Won = wonCount;
            }
            PlayersLock.Release();
            if (name != null)
                await SendMessage("Game", $"{name} ha vinto!");
        }

        /// <summary>
        /// Ritorna la lista con i giocatori che hanno vinto
        /// </summary>
        /// <returns></returns>
        private async Task<Dictionary<int, string>> GetWinningPlayers()
        {
            Dictionary<int, string> won = [];
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
                if (player.Value.Player.Won is int win)
                    won.Add(win, player.Value.Player.Name);
            PlayersLock.Release();
            return won;
        }

        private async Task UpdateTurn(uint playerTurnId, Card tableCard, bool isLeftToRight)
        {
            await SendToClients(new TurnUpdate(playerTurnId, tableCard, isLeftToRight, await GetPlayersCardsNum()));
            await PlayersLock.WaitAsync();
            foreach (var (id, playerData) in Players)
                if (playerData.Player.IsOnline)
                    await SendToClient(new TurnUpdate(playerData.Deck.Cards), id);
            PlayersLock.Release();
        }

        /// <summary>
        /// Gestisce il gioco.
        /// </summary>
        /// <returns></returns>
        private async Task GameMaster(CancellationToken canc)
        {
            // Aspetta che il gioco venga fatto partire
            try
            {
                while (!HasStarted)
                    await Task.Delay(1, canc);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            // Stato iniziale della partita

            // Carta iniziale sul tavolo
            var tableCard = Card.PickNormalRandom();

            // Senso di rotazione del gioco
            bool isLeftToRight = true;

            // Player iniziale
            uint playerTurnId = ADMIN_ID;

            // Carte da aggiungere al prossimo giocatore
            uint addCards = 0;

            // ID del giocatore che ha mandato un +4 e carta che era sul tavolo in quel momento
            (uint, Card)? gavePlusFour = null;

            // Dice se andare avanti con il turno o no
            bool nextTurn = false;

            // Dice se saltare il prossimo giocatore
            bool skipNext = false;

            // Se o no il giocatore corrente ha detto "UNO!"
            bool saidUno = false;

            // Indica quanti giocatori hanno vinto
            int wonCount = 0;

            // Manda carte ai client
            await ResetGame();
            await SendMessage("Game", "Partita avviata!");
            try
            {
                while (true)
                {
                    if (nextTurn || skipNext)
                    {
                        // Imposta il turno al prossimo player
                        playerTurnId = await NextPlayer(playerTurnId, isLeftToRight);

                        // Salta questo player e passa al prossimo
                        if (skipNext)
                        {
                            playerTurnId = await NextPlayer(playerTurnId, isLeftToRight);
                            skipNext = false;
                        }
                        saidUno = false;
                        nextTurn = false;
                    }
                    canc.ThrowIfCancellationRequested();
                    await UpdateTurn(playerTurnId, tableCard, isLeftToRight);
                    var packet = await GameMasterCommunicator.Reader.ReadAsync(canc);
                    if (packet.PacketId == INTERNALCOMM_ID)
                    {
                        switch ((InternalComm)packet.Data)
                        {
                            case InternalComm.UpdateCardsOfPlayer:
                                continue;
                            case InternalComm.SkipPlayer:
                                {
                                    if (packet.PlayerId is uint playerId)
                                    {
                                        if (playerTurnId == playerId)
                                        {
                                            // Evita che il player esca senza prendersi le carte in più
                                            if (addCards > 0)
                                            {
                                                await AddCardsToPlayer(addCards, playerId);
                                                addCards = 0;
                                            }
                                            else if (gavePlusFour != null)
                                            {
                                                await AddCardsToPlayer(4, playerId);
                                                gavePlusFour = null;
                                            }
                                            else await AddCardsToPlayer(1, playerId);
                                            nextTurn = true;
                                        }
                                    }
                                    else Log.Warn($"Il GameMaster ha ricevuto un {nameof(InternalComm.SkipPlayer)} senza player id");
                                }
                                break;
                            case InternalComm.SaidUno:
                                {
                                    if (packet.PlayerId is uint playerId)
                                    {
                                        if (playerTurnId == playerId)
                                            saidUno = true;
                                    }
                                    else Log.Warn($"Il GameMaster ha ricevuto un {nameof(InternalComm.SaidUno)} senza player id");
                                }
                                break;
                            default:
                                Log.Warn($"Il GameMaster ha ricevuto un {nameof(InternalComm)} non valido: {(InternalComm)packet.Data}");
                                break;
                        }
                    }
                    else if ((PacketType)packet.PacketId == PacketType.ActionUpdate)
                    {
                        if (packet.PlayerId is uint playerId)
                        {
                            if (playerId == playerTurnId)
                            {
                                var actionUpdate = (ActionUpdate)packet.Data;
                                if (actionUpdate.CardID is uint cardId)
                                {
                                    // Se il player precedente ha dato un +4 il player deve o pescare o chiamare il bluff
                                    if (gavePlusFour != null)
                                    {
                                        await SendToClient(new GameMessage(MessageType.Info, MessageContent.MustDrawOrCallBluff), playerId);
                                        continue;
                                    }

                                    if (await GetPlayerDeckAndName(playerId) is (Deck deck, string name) && deck.Get(cardId) is Card card)
                                    {
                                        // Se c'è stata una catena di +2 e il giocatore mette un'altra carta il giocatore è forzato a pescare
                                        if (tableCard.NormalType == Normals.PlusTwo && card.NormalType != Normals.PlusTwo)
                                            if (addCards > 0)
                                            {
                                                await AddCardsToPlayer(addCards, playerId);
                                                await SendMessage("Game", $"A {name} sono state date {addCards} carte!");
                                                addCards = 0;
                                                nextTurn = true;
                                                continue;
                                            }
                                        if (tableCard.IsCompatible(card))
                                        {
                                            // Gestisce le carte che hanno un'azione
                                            if (card.NormalType is Normals normalType)
                                            {
                                                if (normalType == Normals.Reverse)
                                                    isLeftToRight = !isLeftToRight;
                                                else if (normalType == Normals.Block)
                                                    skipNext = true;
                                                else if (normalType == Normals.PlusTwo)
                                                    addCards += 2;
                                            }
                                            else if (card.SpecialType is Specials specialType && actionUpdate.CardColor is Colors chosenColor)
                                            {
                                                // Imposta il colore scelto dall'utente
                                                card.Color = chosenColor;

                                                // Salva il nome e la carta corrente di chi ha lanciato il +4
                                                if (specialType == Specials.PlusFour)
                                                    gavePlusFour = (playerId, tableCard);
                                            }

                                            // Imposta la carta sul tavolo
                                            tableCard = card;

                                            // Rimuove la carta
                                            deck.Remove(cardId);
                                            if (deck.Cards.Count == 0)
                                            {
                                                if (saidUno)
                                                {
                                                    wonCount++;
                                                    await PlayerWon(playerId, wonCount);
                                                    nextTurn = true;
                                                    continue;
                                                }
                                                else
                                                {
                                                    await AddCardsToPlayer(2, playerId);
                                                    await SendMessage("Game", $"{name} non ha detto UNO!");
                                                }
                                            }
                                            await SetPlayerDeck(playerId, deck);
                                            nextTurn = true;
                                        }
                                        else
                                        {
                                            await SendToClient(new TurnUpdate(deck.Cards), playerId);
                                            await SendToClient(new GameMessage(MessageType.Error, MessageContent.InvalidCard), playerId);
                                        }
                                    }
                                }
                                else if (actionUpdate.Type is ActionType actionType)
                                {
                                    switch (actionType)
                                    {
                                        case ActionType.Draw:
                                            if (gavePlusFour != null)
                                            {
                                                gavePlusFour = null;
                                                await AddCardsToPlayer(4, playerId);
                                            }
                                            else if (addCards == 0)
                                            {
                                                var card = Card.PickRandom();
                                                if (tableCard.IsCompatible(card) && card.NormalType is Normals normalType)
                                                {
                                                    if (normalType == Normals.Reverse)
                                                        isLeftToRight = !isLeftToRight;
                                                    else if (normalType == Normals.Block)
                                                        skipNext = true;
                                                    else if (normalType == Normals.PlusTwo)
                                                        addCards += 2;
                                                    tableCard = card;
                                                    await SendMessage("Game", "La carta pescata è stata messa nel mazzo.", playerId);
                                                }
                                                else
                                                {
                                                    var (deck, _) = await GetPlayerDeckAndName(playerId);
                                                    deck.Add(card);
                                                    await SetPlayerDeck(playerId, deck);
                                                }
                                            }
                                            else
                                            {
                                                await AddCardsToPlayer(addCards, playerId);
                                                addCards = 0;
                                            }
                                            nextTurn = true;
                                            break;
                                        case ActionType.CallBluff:
                                            if (gavePlusFour is (uint playerWhoGavePlusFour, Card prevCardTable))
                                            {
                                                var (currPlayerDeck, currName) = await GetPlayerDeckAndName(playerId);
                                                var (prevPlayerDeck, prevName) = await GetPlayerDeckAndName(playerWhoGavePlusFour);
                                                if (prevPlayerDeck.CouldSend(prevCardTable))
                                                {
                                                    await AddCardsToPlayer(4, playerWhoGavePlusFour);
                                                    await SendMessage("Game", $"{prevName} stava bluffando! Ha tirato un +4 avendo già altre carte");
                                                }
                                                else
                                                {
                                                    await AddCardsToPlayer(6, playerId);
                                                    await SendMessage("Game", $"{prevName} non stava bluffando! Date 6 carte a {currName}");
                                                }
                                                gavePlusFour = null;
                                                nextTurn = true;
                                            }
                                            else await SendToClient(new GameMessage(MessageType.Info, MessageContent.CannotCallBluff), playerId);
                                            break;
                                        default:
                                            Log.Warn($"ActionType non valido: {actionType}");
                                            break;
                                    }
                                }
                                else Log.Warn($"ActionUpdate mandato dal client non valido: {actionUpdate.Serialize()}");
                            }
                            else await SendToClient(new GameMessage(MessageType.Error, MessageContent.NotYourTurn), playerId);
                        }
                    }
                    else Log.Warn($"Pacchetto non valido mandato al GameMaster: {packet.PacketId}");
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info("Chiusura GameMaster...");
                await SendToClients(new GameEnd(await GetWinningPlayers()));
                await SendMessage("Game", "Il gioco è terminato!");
                HasStarted = false;
            }

            // Avvia un nuovo GameMaster
            GameMasterCancellation = new();
            GameMasterHandler = new Task(async () => await GameMaster(GameMasterCancellation.Token));
            GameMasterHandler.Start();
        }

        /// <summary>
        /// Manda l'ActionUpdate al GameMaster
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task SendToGameMaster(ActionUpdate packet, uint id)
        {
            if (GameMasterCommunicator != null && HasStarted)
                await GameMasterCommunicator.Writer.WriteAsync(new ChannelData(packet.PacketId, packet, id));
        }

        private async Task SendInternalComm(InternalComm comm, uint id)
        {
            if (GameMasterCommunicator != null && HasStarted)
                await GameMasterCommunicator.Writer.WriteAsync(new ChannelData(INTERNALCOMM_ID, comm, id));
        }

        /// <summary>
        /// Manda il pacchetto al socket e gestisce gli errori
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="packet"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        private async Task SendPacketTo<T>(T packet, Socket socket) where T : Serialization<T>
        {
            try
            {
                await Packet.Send(socket, packet);
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
        }

        /// <summary>
        /// Manda un pacchetto a tutti i client.
        /// </summary>
        /// <typeparam name="T">Tipo serializzabile</typeparam>
        /// <param name="packet">Un pacchetto qualsiasi</param>
        /// <returns></returns>
        private async Task BroadcastAll<T>(T packet) where T : Serialization<T>
        {
            await ClientsLock.WaitAsync();
            List<Task> senders = [];
            foreach (var client in Clients)
                if (client.Value != null)
                    senders.Add(Task.Run(async () => await SendPacketTo(packet, client.Value)));
            await Task.WhenAll(senders);
            ClientsLock.Release();
        }

        /// <summary>
        /// Manda un pacchetto a un client specifico.
        /// </summary>
        /// <typeparam name="T">Tipo serializzabile</typeparam>
        /// <param name="id">ID del player a cui mandare il pacchetto</param>
        /// <param name="packet">Un pacchetto qualsiasi</param>
        /// <returns></returns>
        private async Task BroadcastTo<T>(uint id, T packet) where T : Serialization<T>
        {
            // Manda il messaggio al player se è online, altrimenti viene ignorato
            await ClientsLock.WaitAsync();
            if (Clients.TryGetValue(id, out var client))
                if (client != null)
                    await SendPacketTo(packet, client);
            ClientsLock.Release();
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
        /// Manda un messaggio a tutti i client
        /// </summary>
        /// <param name="sender">Chi l'ha mandato</param>
        /// <param name="message">Messaggio</param>
        /// <returns></returns>
        private async Task SendMessage(string sender, string message) => await SendToClients(new ChatMessage($"[{sender}] {message}"));

        /// <summary>
        /// Manda un messaggio a un client specifico
        /// </summary>
        /// <param name="sender">Chi l'ha mandato</param>
        /// <param name="message">Messaggio</param>
        /// <param name="playerId">Player a cui mandare il messaggio</param>
        /// <returns></returns>
        private async Task SendMessage(string sender, string message, uint playerId) => await SendToClient(new ChatMessage($"[{sender}] {message}"), playerId);

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
                                {
                                    var connEnd = (ConnectionEnd)packet.Data;
                                    await BroadcastTo(sendTo, connEnd);
                                    if (connEnd.Final)
                                        await RemovePlayer(sendTo);
                                }
                                else
                                {
                                    await BroadcastAll((ConnectionEnd)packet.Data);
                                    IsBroadcasterRunning = false;
                                    return;
                                }
                            }
                            continue;
                        case PacketType.GameEnd:
                            await BroadcastAll((GameEnd)packet.Data);
                            break;
                        case PacketType.ChatMessage:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (ChatMessage)packet.Data);
                                else
                                    await BroadcastAll((ChatMessage)packet.Data);
                            }
                            break;
                        case PacketType.GameMessage:
                            {
                                if (packet.PlayerId is uint sendTo)
                                    await BroadcastTo(sendTo, (GameMessage)packet.Data);
                                else
                                    await BroadcastAll((GameMessage)packet.Data);
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
        private async Task<uint?> GetId(string name)
        {
            uint? id = null;
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
                if (player.Value.Player.Name == name)
                    id = player.Value.Player.Id;
            PlayersLock.Release();
            return id;
        }

        /// <summary>
        /// Ritorna il numero di giocatori totali
        /// </summary>
        /// <returns></returns>
        private async Task<int> GetPlayersNumTot()
        {
            int n;
            await PlayersLock.WaitAsync();
            n = Players.Count;
            PlayersLock.Release();
            return n;
        }

        /// <summary>
        /// Ritorna il numero dei giocatori non offline
        /// </summary>
        /// <returns></returns>
        private async Task<int> GetPlayersNum()
        {
            int n = 0;
            await PlayersLock.WaitAsync();
            n = Players.Values.Where(player => player.Player.IsOnline).Count();
            PlayersLock.Release();
            return n;
        }

        /// <summary>
        /// Esegue un comando scritto in chat.
        /// </summary>
        /// <param name="id">ID dell'utente che ha fatto il comando</param>
        /// <param name="msg">Contenuto del comando</param>
        /// <returns>Ritorna true se il comando può essere mandato in chat</returns>
        private async Task<bool> ExecCommand(uint id, string msg)
        {
            string[] args = msg.Split(' ');
            if (args[0] == "uno!")
            {
                await SendInternalComm(InternalComm.SaidUno, id);
                return true;
            }
            // I comandi sono riservati all'admin
            if (id != ADMIN_ID)
                return true;
            switch (args[0])
            {
                case ".help":
                    await SendMessage("Cmd", $".help - Mostra questo messaggio{Environment.NewLine}.start - Avvia il gioco{Environment.NewLine}.stop - Termina il gioco{Environment.NewLine}.kick - Disconnette un utente{Environment.NewLine}.remove - Rimuove un utente", id);
                    return false;
                case ".start":
                    if (!HasStarted)
                    {
                        if (await GetPlayersNum() > 1)
                        {
                            await SendMessage("Server", "Avvio del gioco...");
                            HasStarted = true;
                        }
                        else await SendMessage("Cmd", "Il gioco ha bisogno di due o più giocatori", id);
                    }
                    else await SendMessage("Cmd", "Il gioco è già partito.", id);
                    return false;
                case ".stop":
                    if (HasStarted)
                        GameMasterCancellation.Cancel();
                    else await SendMessage("Cmd", "Il gioco non è ancora partito.", id);
                    return false;
                case ".kick":
                    if (args.Length >= 2)
                    {
                        string name = args[1];
                        var _kickId = await GetId(name);
                        if (_kickId is uint kickId)
                        {
                            Log.Info($"Utente da kickare: {kickId}");
                            if (kickId != ADMIN_ID)
                            {
                                await KickPlayer(kickId);
                                await SendMessage("Server", $"{name} è stato kickato");
                            }
                        }
                        else await SendMessage("Cmd", $"L'utente {name} non esiste", id);
                    }
                    else await SendMessage("Cmd", "Comando .kick non valido. Uso: .kick NomePlayer", id);
                    return false;
                case ".remove":
                    if (args.Length >= 2)
                    {
                        string name = args[1];
                        var _removeId = await GetId(name);
                        if (_removeId is uint removeId)
                        {
                            Log.Info($"Utente da rimuovere: {removeId}");
                            if (removeId != ADMIN_ID)
                            {
                                await RemovePlayer(removeId);
                                await SendMessage("Server", $"{name} è stato rimosso");
                            }
                        }
                        else await SendMessage("Cmd", $"L'utente {name} non esiste", id);
                    }
                    else await SendMessage("Cmd", "Comando .remove non valido. Uso: .remove NomePlayer", id);
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

            try
            {
                while (true)
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
                        case PacketType.ActionUpdate:
                            {
                                var packet = await Packet.Receive<ActionUpdate>(client);
                                await SendToGameMaster(packet, userId);
                            }
                            continue;
                        default:
                            Log.Warn(client, $"Client ha mandato un packet type non valido: {packetType}");
                            await Packet.CancelReceive(client);
                            continue;
                    }
                }
            }
            catch (PacketException e)
            {
                Log.Error(addr, $"Packet exception durante l'handling di un client: {e}");
            }
            catch (Exception e) when (e is OperationCanceledException || e is ObjectDisposedException)
            {
                Log.Info(addr, "Chiusura di questo client handler...");
                if (await ShouldRemove(userId))
                    goto abandon;
            }
            catch (Exception e)
            {
                Log.Error(addr, $"Errore durante l'handling di un client: {e}");
            }

        // Disconnessione possibilmente temporanea del client
        // I dati del giocatore vengono mantenuti in memoria.
        close:
            Log.Info(addr, "Disconnessione client...");
            await CloseClient(userId);

            // Manda l'update della disconnessione
            await SendMessage("Server", $"{name} si è disconnesso");
            return;

        // Disconnessione permanente del client.
        // I dati del giocatore vengono rimossi dalla memoria.
        abandon:
            Log.Info(addr, "Rimozione client...");
            await RemovePlayerData(userId);

            // Manda l'update della rimozione del player
            await SendMessage("Server", $"{name} ha abbandonato");
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
            await ClientsLock.WaitAsync();
            if (Clients.TryGetValue(id, out var client))
                client.Close();
            Clients.Remove(id);
            ClientsLock.Release();

            // Imposta il player come offline e cancella l'handler del client
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var player))
                player.Player.IsOnline = false;
            PlayersLock.Release();

            // Aggiorna lo stato della partita e dei giocatori
            await SendInternalComm(InternalComm.SkipPlayer, id);
            await UpdatePlayer(id, false);
        }

        /// <summary>
        /// Rimuove un player
        /// </summary>
        /// <param name="id">ID del player da rimuovere</param>
        /// <returns></returns>
        private async Task RemovePlayerData(uint id)
        {
            // Chiude la connessione
            await ClientsLock.WaitAsync();
            if (Clients.TryGetValue(id, out var client))
                client.Close();
            Clients.Remove(id);
            ClientsLock.Release();

            // Rimuove il giocatore
            await PlayersLock.WaitAsync();
            Players.Remove(id);
            PlayersLock.Release();

            // Aggiorna lo stato della partita e dei giocatori
            await SendInternalComm(InternalComm.SkipPlayer, id);
            await UpdatePlayers();
        }

        /// <summary>
        /// Kicka il player dalla partita
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task KickPlayer(uint id)
        {
            await SendToClient(new ConnectionEnd(false, "Sei stato kickato, potrai riunirti in seguito"), id);
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var playerData))
            {
                playerData.Remove = false;
                playerData.Cancellation.Cancel();
            }
            PlayersLock.Release();
        }

        /// <summary>
        /// Dice se il player deve essere rimosso
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task<bool> ShouldRemove(uint id)
        {
            bool remove = true;
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var playerData))
                remove = playerData.Remove;
            PlayersLock.Release();
            return remove;
        }

        /// <summary>
        /// Causa la rimozione di un player
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private async Task RemovePlayer(uint id)
        {
            await SendToClient(new ConnectionEnd(true, "Sei stato rimosso dalla partita"), id);
            await PlayersLock.WaitAsync();
            if (Players.TryGetValue(id, out var playerData))
            {
                playerData.Remove = true;
                playerData.Cancellation.Cancel();
            }
            PlayersLock.Release();
        }


        /// <summary>
        /// Manda ai client la lista aggiornata di tutti i player
        /// </summary>
        /// <returns></returns>
        private async Task UpdatePlayers()
        {
            var players = new List<Player>();
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
                players.Add((Player)player.Value.Player.Clone());
            PlayersLock.Release();
            var playersUpdate = new PlayersUpdate(players, null, null);
            await SendToClients(playersUpdate);
        }

        /// <summary>
        /// Aggiorna la lista dei player a un player specifico
        /// </summary>
        /// <param name="id">ID del player a cui mandare l'update</param>
        /// <returns></returns>
        private async Task UpdatePlayersTo(uint id)
        {
            var players = new List<Player>();
            await PlayersLock.WaitAsync();
            foreach (var player in Players)
                players.Add((Player)player.Value.Player.Clone());
            PlayersLock.Release();
            var playersUpdate = new PlayersUpdate(players, null, null);
            await SendToClient(playersUpdate, id);
        }

        /// <summary>
        /// Aggiorna i client dello status di un giocatore specifico
        /// </summary>
        /// <param name="id">ID del giocatore</param>
        /// <param name="isOnline">Se è online o no</param>
        /// <returns></returns>
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
                        // Se supera il numero massimo di player non accetta la connessione
                        if (await GetPlayersNumTot() >= MaxPlayers)
                        {
                            var status = new JoinStatus($"Ci sono troppi player nel server. Numero massimo: {MaxPlayers}");
                            await Packet.Send(client, status);
                            goto close;
                        }
                        // Se il nome è già usato non accetta la connessione
                        if (await GetId(joinRequest.NewPlayer.Name) != null)
                        {
                            var status = new JoinStatus($"Il nome {joinRequest.NewPlayer.Name} è già stato preso");
                            await Packet.Send(client, status);
                            goto close;
                        }
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
                            await PlayersLock.WaitAsync();
                            Players.Add(newID, playerData);
                            PlayersLock.Release();

                            // Manda i nuovi dati generati dal server (ID e Access Code)
                            var status = new JoinStatus(playerData.Player, playerData.AccessCode);
                            await Packet.Send(client, status);

                            // Avvia il client handler
                            clientHandler.Start();

                            // Aggiunta connessione
                            await ClientsLock.WaitAsync();
                            Clients.Add(newID, client);
                            ClientsLock.Release();

                            // Manda la nuova lista dei player
                            await UpdatePlayers();
                            await SendMessage("Server", $"Un nuovo player si è unito: {playerData.Player.Name}");
                            await SendToClient(MotdMessage, newID);

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
                            await PlayersLock.WaitAsync();
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
                                    PlayersLock.Release();
                                    goto invalid;
                                }
                            }
                            // Se l'utente non esiste la richiesta non è valida
                            else
                            {
                                PlayersLock.Release();
                                goto invalid;
                            }
                            PlayersLock.Release();

                            // Manda il nuovo access code
                            var rejoinStatusOk = new JoinStatus(newAccessCode);
                            await Packet.Send(client, rejoinStatusOk);

                            // Avvia il client handler
                            clientHandler.Start();

                            // Aggiunta connessione
                            await ClientsLock.WaitAsync();
                            Clients.Add(userId, client);
                            ClientsLock.Release();

                            // Manda la lista dei player al giocatore
                            if (userId == ADMIN_ID)
                            {
                                await UpdatePlayers();
                                await SendMessage("Server", $"Benvenuto nel gioco, {name}.");
                            }
                            else
                            {
                                await UpdatePlayersTo(userId);
                                await UpdatePlayer(userId, true);
                                await SendInternalComm(InternalComm.UpdateCardsOfPlayer, userId);
                                await SendMessage("Server", $"{name} si è riconnesso");
                            }
                            await SendToClient(MotdMessage, userId);

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
            IPEndPoint ipEndpoint = new(IPAddress.Parse(Address), Port);
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
