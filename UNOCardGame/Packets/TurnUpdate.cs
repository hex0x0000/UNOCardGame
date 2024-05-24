using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Direzione dei turni
    /// </summary>
    public enum TurnDirection
    {
        RighToLeft,
        LeftToRight
    }

    public class TurnUpdate : Serialization<TurnUpdate>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.TurnUpdate;

        /// <summary>
        /// ID del player di questo turno
        /// </summary>
        public uint PlayerId { get; }

        /// <summary>
        /// Carta sul tavolo in questo turno
        /// </summary>
        public Card TableCard { get; }

        /// <summary>
        /// Senso della direzione dei turni, se da sinistra a destra o destra a sinistra
        /// </summary>
        public TurnDirection Direction { get; }

        [JsonConstructor]
        public TurnUpdate(uint playerUpdate, Card tableCard, TurnDirection turnDirection)
        {
            PlayerId = playerUpdate; TableCard = tableCard; Direction = turnDirection;
        }
    }
}
