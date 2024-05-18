using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Risultato della richiesta Join.
    /// Contiene due payload a seconda se la richiesta è andata bene (Ok) o se c'è stato un errore (Err).
    /// </summary>
    public class JoinStatus : Serialization<JoinStatus>
    {
        public override short PacketId => (short)PacketType.JoinStatus;

        private static readonly int _StatusCodeEnumLength = Enum.GetValues(typeof(JoinType)).Length;

        /// <summary>
        /// Informazioni del nuovo player
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Player Player { get; } = null;

        /// <summary>
        /// Nuovo access code
        /// </summary>
        public long? AccessCode { get; } = null;

        /// <summary>
        /// Contenuto del payload quando la richiesta Join andata male.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Err { get; } = null;

        public JoinStatus(Player player, long accessCode)
        {
            Player = player;
            AccessCode = accessCode;
        }

        public JoinStatus(long accessCode) => AccessCode = accessCode;

        public JoinStatus(string payloadErr) => Err = payloadErr;

        [JsonConstructor]
        public JoinStatus(Player player, long accessCode, string payloadErr)
        {
            Player = player; AccessCode = accessCode; Err = payloadErr;
        }
    }
}
