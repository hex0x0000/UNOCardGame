using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Specifica l'azione dell'utente durante la connessione,
    /// se unirsi o riunirsi dopo una disconessione forzata.
    /// </summary>
    public enum JoinType
    {
        Join,
        Rejoin
    }

    /// <summary>
    /// Classe Join, mandata all'inizio della connessione per unirsi al server.
    /// </summary>
    public class Join : Serialization<Join>
    {
        public override short PacketId => 0;

        private static readonly int _JoinTypeEnumLength = Enum.GetValues(typeof(JoinType)).Length;

        private JoinType _Type;

        /// <summary>
        /// Specifica se l'utente vuole connettersi o riconnettersi.
        /// </summary>
        public JoinType Type
        {
            get => _Type;
            private set
            {
                if ((int)value < 0 || (int)value >= _JoinTypeEnumLength)
                    throw new ArgumentOutOfRangeException(nameof(JoinType), "Enum must stay within its range");
                _Type = value;
            }
        }

        /// <summary>
        /// Se la connessione è nuova, manda un nuovo oggetto Player contenente le informazioni del player.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Player NewPlayer { get; }

        /// <summary>
        /// Se si tratta di una riconnessione, questo codice è necessario per l'accesso al vecchio ID.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ulong? AccessCode { get; }

        /// <summary>
        /// ID della connessione precedente, deve essere accompagnato dall'access code per riaccedere.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; }

        /// <summary>
        /// Metodo util per ritornare il PacketId in maniera statica
        /// </summary>
        /// <returns>Packet ID di questa classe</returns>
        public static short GetPacketId() => new Join().PacketId;

        private Join() { }

        [JsonConstructor]
        public Join(JoinType type, Player newPlayer, ulong? accessCode, uint? id)
        {
            Type = type; NewPlayer = newPlayer; AccessCode = accessCode; Id = id;
        }
    }
}
