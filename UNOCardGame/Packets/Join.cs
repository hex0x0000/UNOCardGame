﻿using System;
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
    /// Classe Join, mandata all'inizio della connessione dal client per unirsi al server.
    /// </summary>
    public class Join : Serialization<Join>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.Join;

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
        public Player NewPlayer { get; } = null;

        /// <summary>
        /// Se si tratta di una riconnessione, questo codice è necessario per l'accesso al vecchio ID.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? AccessCode { get; } = null;

        /// <summary>
        /// ID della connessione precedente, deve essere accompagnato dall'access code per riaccedere.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; } = null;

        /// <summary>
        /// Richiesta di unirsi al gioco
        /// </summary>
        /// <param name="newPlayer">I dati del nuovo player</param>
        public Join(Player newPlayer)
        {
            Type = JoinType.Join;
            NewPlayer = newPlayer;
        }

        /// <summary>
        /// Richiesta di riunirsi al gioco.
        /// </summary>
        /// <param name="id">ID del giocatore</param>
        /// <param name="accessCode">Codice di accesso del giocatore</param>
        public Join(uint id, long accessCode)
        {
            Type = JoinType.Rejoin;
            AccessCode = accessCode;
            Id = id;
        }

        [JsonConstructor]
        public Join(JoinType type, Player newPlayer, long? accessCode, uint? id)
        {
            Type = type; NewPlayer = newPlayer; AccessCode = accessCode; Id = id;
        }
    }
}
