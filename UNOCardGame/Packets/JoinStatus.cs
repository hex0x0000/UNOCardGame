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
    /// </summary>
    public enum StatusCode
    {
        Success,
        Error
    }

    /// <summary>
    /// Risultato della richiesta Join.
    /// Contiene due payload a seconda se la richiesta è andata bene (Ok) o se c'è stato un errore (Err).
    /// </summary>
    public class JoinStatus : Serialization<JoinStatus>
    {
        public override short PacketId => 1;

        private static readonly int _StatusCodeEnumLength = Enum.GetValues(typeof(JoinType)).Length;

        private StatusCode _Code;

        /// <summary>
        /// Specifica se la richiesta è andata bene o male.
        /// </summary>
        public StatusCode Code
        {
            get => _Code; private set
            {
                if ((int)value < 0 || (int)value >= _StatusCodeEnumLength)
                    throw new ArgumentOutOfRangeException(nameof(JoinType), "Enum must stay within its range");
                _Code = value;
            }
        }

        /// <summary>
        /// Contenuto del payload se la richiesta Join andata bene.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public NewPlayerData Ok { get; }

        /// <summary>
        /// Contenuto del payload quando la richiesta Join andata male.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Err { get; }

        /// <summary>
        /// Metodo util per ritornare il PacketId in maniera statica
        /// </summary>
        /// <returns>Packet ID di questa classe</returns>
        public static short GetPacketId() => new JoinStatus().PacketId;

        private JoinStatus() { }

        public JoinStatus(StatusCode code)
        {
            Code = code; Ok = null; Err = null;
        }

        public JoinStatus(NewPlayerData payloadOk)
        {
            Code = StatusCode.Success; Ok = payloadOk; Err = null;
        }

        public JoinStatus(string payloadErr)
        {
            Code = StatusCode.Error; Err = payloadErr; Ok = null;
        }

        [JsonConstructor]
        public JoinStatus(StatusCode code, NewPlayerData payloadOk, string payloadErr)
        {
            Code = code; Ok = payloadOk; Err = payloadErr;
        }
    }
}
