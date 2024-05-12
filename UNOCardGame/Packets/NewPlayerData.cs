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

        [JsonConstructor]
        public NewPlayerData(Player player, ulong accessCode) { Player = player; AccessCode = accessCode; }
    }
}
