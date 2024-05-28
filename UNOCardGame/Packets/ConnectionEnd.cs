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
    [method: JsonConstructor]
    public class ConnectionEnd(bool final, string message) : Serialization<ConnectionEnd>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.ConnectionEnd;

        /// <summary>
        /// Disconnessione definitiva, se impostato a vero riconnettersi non è possibile
        /// </summary>
        public bool Final { get; } = final;
        /// <summary>
        /// Messaggio mandato dal server dopo la disconnessione
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Message { get; } = message;
    }
}
