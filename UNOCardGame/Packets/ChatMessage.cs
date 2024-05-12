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
    public class ChatMessage : Serialization<ChatMessage>
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? FromId { get; }

        public string Message { get; }

        [JsonConstructor]
        public ChatMessage(uint? fromId, string message) 
        {
            FromId = fromId; Message = message;
        }
    }
}
