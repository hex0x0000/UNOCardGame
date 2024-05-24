using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Termina la connessione
    /// </summary>
    public class ConnectionEnd : Serialization<ConnectionEnd>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.ConnectionEnd;

        /// <summary>
        /// Disconnessione definitiva, se impostato a vero riconnettersi non è possibile
        /// </summary>
        public bool Final { get; } = false;

        /// <summary>
        /// Messaggio mandato dal server dopo la disconnessione
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Message { get; }

        [JsonConstructor]
        public ConnectionEnd(bool final, string message)
        { Final = final; Message = message; }
    }
}
