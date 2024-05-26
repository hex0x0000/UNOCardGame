using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UNOCardGame.Packets
{
    public enum MessageType { 
        Error,
        Info
    }

    public enum MessageContent {
        InvalidCard,
        NotYourTurn,
        MustDrawOrCallBluff,
        CannotCallBluff
    }

    /// <summary>
    /// Messaggio del gioco mandato al giocatore
    /// </summary>
    public class GameMessage : Serialization<GameMessage>
    {
        [JsonIgnore]
        public override short PacketId => (short)PacketType.GameMessage;

        /// <summary>
        /// Dice se il messaggio è un errore o un'informazione
        /// </summary>
        public MessageType Type { get; }

        /// <summary>
        /// Contenuto del messaggio
        /// </summary>
        public MessageContent Content { get; }

        [JsonConstructor]
        public GameMessage(MessageType type, MessageContent content)
        {
            Type = type; Content = content;
        }

        public override string ToString()
        {
            switch (Content)
            {
                case MessageContent.InvalidCard:
                    return "Questa carta non può essere messa sul tavolo. Se non puoi mettere nessuna carta devi pescare per andare avanti con il turno.";
                case MessageContent.NotYourTurn:
                    return "Questo non è il tuo turno.";
                case MessageContent.MustDrawOrCallBluff:
                    return "Devi pescare le carte per continuare oppure chiamare il bluff";
                case MessageContent.CannotCallBluff:
                    return "Puoi chiamare il bluff solo quando ti viene dato un +4";
            }
            return $"Messaggio sconosciuto: {Content}";
        }
    }
}
