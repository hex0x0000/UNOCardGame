using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Classe che aggiorna lo stato delle carte dei giocatori durante la partita
    /// </summary>
    public class CardsUpdate : Serialization<CardsUpdate>
    {
        public override short PacketId => (short)PacketType.CardsUpdate;

        /// <summary>
        /// Carta mandata dal client al server
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? CardID { get; } = null;

        /// <summary>
        /// Carte del giocatore mandate dal server al client
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Card> NewCards { get; } = null;

        /// <summary>
        /// Numero di carte di ogni giocatore
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<uint, uint> PlayersCardsNum { get; } = null;

        /// <summary>
        /// Carta mandata dal giocatore al server
        /// </summary>
        /// <param name="cardId">ID della carta</param>
        public CardsUpdate(uint cardId) => CardID = cardId;

        /// <summary>
        /// Carte mandate dal server al giocatore
        /// </summary>
        /// <param name="newCards">Lista delle carte del giocatore</param>
        public CardsUpdate(List<Card> newCards) => NewCards = newCards;

        /// <summary>
        /// Numero di carte di ogni giocatore
        /// </summary>
        /// <param name="playersCardsNum">Dictionary con l'ID del giocatore e il numero di carte</param>
        public CardsUpdate(Dictionary<uint, uint> playersCardsNum) => PlayersCardsNum = playersCardsNum;

        [JsonConstructor]
        public CardsUpdate(uint? cardId, List<Card> newCards, Dictionary<uint, uint> playersCardsNum)
        {
            CardID = cardId; NewCards = newCards; PlayersCardsNum = playersCardsNum;
        }
    }
}
