using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Dati del player generati dal server mandati al client.
    /// Contiene la classe player e l'access code.
    /// </summary>
    public class NewPlayerData : Serialization<NewPlayerData>
    {
        public Player Player { get; }
        public ulong AccessCode { get; }

        public override short PacketId => 3;

        /// <summary>
        /// Metodo util per ritornare il PacketId in maniera statica
        /// </summary>
        /// <returns>Packet ID di questa classe</returns>
        public static short GetPacketId() => new NewPlayerData().PacketId;

        private NewPlayerData() { }

        [JsonConstructor]
        public NewPlayerData(Player player, ulong accessCode) { Player = player; AccessCode = accessCode; }
    }
}
