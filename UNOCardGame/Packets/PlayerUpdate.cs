using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    public enum PlayerUpdateType {
        /// <summary>
        /// Mandato quando si aggiunge un nuovo player.
        /// </summary>
        NewPlayer,

        /// <summary>
        /// Mandato quando è necessario un update dell'intera lista dei player.
        /// Di solito all'inizio del gioco.
        /// </summary>
        PlayersUpdate,

        /// <summary>
        /// Aggiorna il numero di carte di un player.
        /// </summary>
        CardsNumUpdate,

        /// <summary>
        /// Aggiorna lo status di un giocatore da online a offline o viceversa.
        /// </summary>
        OnlineStatusUpdate,

        /// <summary>
        /// Rimozione di un player.
        /// </summary>
        RemoveUpdate
    }

    /// <summary>
    /// Update da parte del server relativo alle informazioni degli altri player.
    /// </summary>
    public class PlayerUpdate : Serialization<PlayerUpdate>
    {
        public override short PacketId => (short)PacketType.PlayerUpdate;

        /// <summary>
        /// Tipo dell'update.
        /// </summary>
        public PlayerUpdateType Type { get; }

        /// <summary>
        /// Lista dei player se si tratta di un PlayersUpdate
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Player> Players { get; }

        /// <summary>
        /// Player se si tratta di un Player
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Player Player { get; }

        /// <summary>
        /// ID se si tratta di un CardsNumUpdate, OnlineStatusUpdate o RemoveUpdate
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; }

        /// <summary>
        /// Numero di carte se si tratta di un CardsNumUpdate
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CardsNum { get; }

        /// <summary>
        /// Status della connessione del player se si tratta di un OnlineStatusUpdate
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsOnline { get; }

        /// <summary>
        /// Nuovo player nel gioco.
        /// </summary>
        /// <param name="player">Nuovo Player</param>
        public PlayerUpdate(Player player) {
            Type = PlayerUpdateType.NewPlayer;
            Player = player;
        }

        /// <summary>
        /// Update dell'intera lista di player nel gioco.
        /// </summary>
        /// <param name="players">Tutti i player nel server</param>
        public PlayerUpdate(List<Player> players)
        {
            Type = PlayerUpdateType.PlayersUpdate;
            Players = players;
        }

        /// <summary>
        /// Update del numero di carte di un player.
        /// </summary>
        /// <param name="id">ID del player</param>
        /// <param name="cardsNum">Numero di carte</param>
        public PlayerUpdate(uint id, int cardsNum)
        {
            Type = PlayerUpdateType.CardsNumUpdate;
            Id = id;
            CardsNum = cardsNum;
        }

        /// <summary>
        /// Update dello status della connessione del player.
        /// </summary>
        /// <param name="id">ID del player</param>
        /// <param name="isOnline">Status della connessione</param>
        public PlayerUpdate(uint id, bool isOnline)
        {
            Type = PlayerUpdateType.OnlineStatusUpdate;
            Id = id;
            IsOnline = isOnline;
        }

        /// <summary>
        /// Rimozione di un player.
        /// </summary>
        /// <param name="id">ID del player da rimuovere</param>
        public PlayerUpdate(uint id)
        {
            Type = PlayerUpdateType.RemoveUpdate;
            Id = id;
        }

        [JsonConstructor]
        public PlayerUpdate(PlayerUpdateType type, Player player, List<Player> players, uint? id, bool? isOnline, int? cardsNum)
        {
            Type = type; Player = player; Players = players; Id = id; IsOnline = isOnline; CardsNum = cardsNum;
        } 
    }
}
