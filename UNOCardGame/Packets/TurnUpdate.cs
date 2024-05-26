using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Aggiorna lo stato del gioco e del turno, mandato dal server ai giocatori
    /// </summary>
    public class TurnUpdate : Serialization<TurnUpdate>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.TurnUpdate;

        /// <summary>
        /// ID del player di questo turno
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? PlayerId { get; }

        /// <summary>
        /// Carta sul tavolo in questo turno
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Card TableCard { get; }

        /// <summary>
        /// Senso della direzione dei turni, se da sinistra a destra o destra a sinistra
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsLeftToRight { get; }

        /// <summary>
        /// Numero di carte di ogni giocatore
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<uint, int> PlayersCardsNum { get; } = null;

        /// <summary>
        /// Nuove carte del giocatore
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Card> NewCards { get; } = null;

        /// <summary>
        /// Carte del mazzo mandate al singolo giocatore
        /// </summary>
        /// <param name="newCards">Lista delle carte del giocatore</param>
        public TurnUpdate(List<Card> newCards) => NewCards = newCards;

        /// <summary>
        /// Update dello stato della partita a tutti i giocatori
        /// </summary>
        /// <param name="playersCardsNum">Dictionary con l'ID del giocatore e il numero di carte</param>
        public TurnUpdate(uint playerId, Card tableCard, bool isLeftToRight, Dictionary<uint, int> playersCardsNum)
        {
            PlayersCardsNum = playersCardsNum; PlayerId = playerId; TableCard = tableCard; IsLeftToRight = isLeftToRight;
        }

        [JsonConstructor]
        public TurnUpdate(uint? playerId, Card tableCard, bool? isLeftToRight, Dictionary<uint, int> playersCardsNum, List<Card> newCards)
        {
            PlayerId = playerId; TableCard = tableCard; IsLeftToRight = isLeftToRight; PlayersCardsNum = playersCardsNum; NewCards = newCards;
        }
    }
}
