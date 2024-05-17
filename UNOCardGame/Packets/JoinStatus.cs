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
    /// Dati del player generati dal server mandati al client.
    /// Contiene la classe player e l'access code.
    /// </summary>
    public class NewPlayerData
    {
        public Player Player { get; }

        public ulong AccessCode { get; }

        [JsonConstructor]
        public NewPlayerData(Player player, ulong accessCode) { Player = player; AccessCode = accessCode; }

    }

    /// <summary>
    /// Risultato della richiesta Join.
    /// Contiene due payload a seconda se la richiesta è andata bene (Ok) o se c'è stato un errore (Err).
    /// </summary>
    public class JoinStatus : Serialization<JoinStatus>
    {
        public override short PacketId => (short)PacketType.JoinStatus;

        private static readonly int _StatusCodeEnumLength = Enum.GetValues(typeof(JoinType)).Length;

        /// <summary>
        /// Contenuto del payload se la richiesta Join andata bene.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public NewPlayerData Ok { get; } = null;

        /// <summary>
        /// Contenuto del payload quando la richiesta Join andata male.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Err { get; } = null;

        public JoinStatus(NewPlayerData payloadOk) => Ok = payloadOk;

        public JoinStatus(string payloadErr) => Err = payloadErr;

        [JsonConstructor]
        public JoinStatus(NewPlayerData payloadOk, string payloadErr)
        {
            Ok = payloadOk; Err = payloadErr;
        }
    }
}
