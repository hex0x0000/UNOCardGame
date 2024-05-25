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
        CallBluff,
        SaidUno
    }

    /// <summary>
    /// Manda al server l'azione del client
    /// </summary>
    public class ActionUpdate : Serialization<ActionUpdate>
    {
        public override short PacketId => (short)PacketType.ActionUpdate;

        /// <summary>
        /// Carta mandata dal client al server
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? CardID { get; }

        /// <summary>
        /// Tipo di azione del giocatore
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ActionType? Type { get; }

        [JsonConstructor]
        public ActionUpdate(uint? cardId, ActionType? type)
        {
            CardID = cardId; Type = type;
        }
    }
}
