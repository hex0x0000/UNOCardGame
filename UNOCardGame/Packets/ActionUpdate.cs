using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    public enum ActionType
    {
        Draw,
        CallBluff
    }

    /// <summary>
    /// Manda al server l'azione del client
    /// </summary>
    [method: JsonConstructor]
    public class ActionUpdate(uint? cardId, Colors? cardColor, ActionType? type) : Serialization<ActionUpdate>
    {
        public override short PacketId => (short)PacketType.ActionUpdate;

        /// <summary>
        /// Carta mandata dal client al server
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? CardID { get; } = cardId;
        /// <summary>
        /// Specifica il colore della carta se è una carta speciale
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Colors? CardColor { get; } = cardColor;
        /// <summary>
        /// Tipo di azione del giocatore
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ActionType? Type { get; } = type;
    }
}
