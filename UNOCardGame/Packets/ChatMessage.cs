using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Pacchetto che contiene un messaggio della chat.
    /// </summary>
    public class ChatMessage : Serialization<ChatMessage>, ICloneable
    {
        /// <summary>
        /// ID del giocatore che ha mandato il messaggio.
        /// Null se il messaggio è stato mandato dal server.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? FromId { get; set; }

        /// <summary>
        /// Contenuto del messaggio.
        /// </summary>
        public string Message { get; }

        public override short PacketId => 2;

        /// <summary>
        /// Metodo util per ritornare il PacketId in maniera statica
        /// </summary>
        /// <returns>Packet ID di questa classe</returns>
        public static short GetPacketId() => new ChatMessage().PacketId;

        public object Clone() => new ChatMessage(FromId, (string)Message.Clone());

        private ChatMessage() { }

        [JsonConstructor]
        public ChatMessage(uint? fromId, string message) 
        {
            FromId = fromId; Message = message;
        }
    }
}
