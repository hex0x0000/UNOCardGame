using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        [JsonIgnore]
        public const int MSG_MAX_CHARS = 200;

        [JsonIgnore]
        public override short PacketId => (short)PacketType.ChatMessage;

        /// <summary>
        /// ID del giocatore che ha mandato il messaggio.
        /// Null se il messaggio è stato mandato dal server.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? FromId { get; set; } = null;

        private string _Message;

        /// <summary>
        /// Contenuto del messaggio.
        /// </summary>
        public string Message
        {
            get => _Message;
            private set => _Message = MsgCut(value.Trim().Replace("\n", ""));
        }

        /// <summary>
        /// Nuovo messaggio senza FromId
        /// </summary>
        /// <param name="message"></param>
        public ChatMessage(string message) => Message = message;

        [JsonConstructor]
        public ChatMessage(uint? fromId, string message)
        {
            FromId = fromId; Message = message;
        }

        private static string MsgCut(string value) => value.Length <= MSG_MAX_CHARS ? value : value.Substring(0, MSG_MAX_CHARS);
    }
}
