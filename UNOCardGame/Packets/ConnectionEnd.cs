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
        /// Rimuove il giocatore dal server
        /// </summary>
        public bool Abandon { get; } = false;

        [JsonConstructor]
        public ConnectionEnd(bool abandon) => Abandon = abandon;
    }
}
