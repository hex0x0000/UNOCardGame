using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Pacchetto mandato al client per dire che è terminato il gioco, con anche la classifica dei giocatori
    /// </summary>
    public class GameEnd : Serialization<GameEnd>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.GameEnd;

        /// <summary>
        /// Lista dei vincitori. Numero nella classifica e nome
        /// </summary>
        public Dictionary<int, string> Winners { get; } = [];

        [JsonConstructor]
        public GameEnd(Dictionary<int, string> winners) => Winners = winners;

        public override string ToString()
        {
            if (Winners.Count == 0) return "Nessuno ha vinto.";
            string winners = $"Classifica:{Environment.NewLine}";
            foreach ((int win, string name) in Winners)
                winners += $"{win}. {name}{Environment.NewLine}";
            return winners;
        }
    }
}
