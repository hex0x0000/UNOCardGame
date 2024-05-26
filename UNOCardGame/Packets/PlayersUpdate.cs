using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    /// <summary>
    /// Update da parte del server relativo alle informazioni degli altri player.
    /// </summary>
    public class PlayersUpdate : Serialization<PlayersUpdate>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.PlayerUpdate;

        /// <summary>
        /// Lista dei player se si tratta di un PlayersUpdate
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Player> Players { get; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsOnline { get; }

        /// <summary>
        /// Update dei player nel gioco
        /// </summary>
        /// <param name="players">Tutti i player nel server</param>
        public PlayersUpdate(List<Player> players, uint? id, bool? isOnline)
        {
            Players = players; Id = id; IsOnline = isOnline;
        }
    }
}
