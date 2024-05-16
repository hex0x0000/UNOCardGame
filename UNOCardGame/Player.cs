using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    /// <summary>
    /// Personalizzazioni del giocatore.
    /// </summary>
    public class Personalization : ICloneable
    {
        /// <summary>
        /// Colori possibili per il background dei giocatori.
        /// </summary>
        [JsonIgnore]
        public static readonly List<Color> BG_COLORS = new List<Color>() { Color.White, Color.Beige, Color.DimGray };

        /// <summary>
        /// Colori possibili per l'username dei giocatori.
        /// </summary>
        [JsonIgnore]
        public static readonly List<Color> USERNAME_COLORS = new List<Color> { Color.Black, Color.DeepSkyBlue, Color.DarkRed };

        private Color _UsernameColor;

        /// <summary>
        /// Colore dell'username.
        /// </summary>
        public Color UsernameColor
        {
            get => _UsernameColor;
            private set
            {
                if (!USERNAME_COLORS.Contains(value))
                    throw new ArgumentException("Value must be contained within its possible values specified in USERNAME_COLORS", nameof(UsernameColor));
                _UsernameColor = value;
            }
        }

        private Color _BackgroundColor;

        /// <summary>
        /// Colore del background dell'utente.
        /// </summary>
        public Color BackgroundColor
        {
            get => _BackgroundColor;
            private set
            {
                if (!BG_COLORS.Contains(value))
                    throw new ArgumentException("Value must be contained within its possible values specified in BG_COLORS", nameof(BackgroundColor));
                _BackgroundColor = value;
            }
        }

        /// <summary>
        /// Nome del file dell'immagine profilo.
        /// </summary>
        public string AvatarImage { get; }

        /// <summary>
        /// Inizializza una nuova personalizzazione
        /// </summary>
        /// <param name="usernameColor">Colore dell'username</param>
        /// <param name="backgroundColor">Colore del background dell'username</param>
        /// <param name="avatarImage">Nome del file dell'immagine profilo (senza estensione)</param>
        [JsonConstructor]
        public Personalization(Color usernameColor, Color backgroundColor, string avatarImage)
        {
            UsernameColor = usernameColor;
            BackgroundColor = backgroundColor;
            AvatarImage = avatarImage.Replace("/", "").Replace("\\", ""); // Evita che vengano messi input che modificano il path
        }

        /// <summary>
        /// Inizializza una nuova personalizzazione con colori random e l'immagine
        /// profilo di default.
        /// </summary>
        public Personalization()
        {
            var random = new Random();
            BackgroundColor = BG_COLORS[random.Next(BG_COLORS.Count)];
            UsernameColor = USERNAME_COLORS[random.Next(USERNAME_COLORS.Count)];
            AvatarImage = "default";
        }

        public object Clone() => new Personalization(UsernameColor, BackgroundColor, (string)AvatarImage.Clone());
    }

    /// <summary>
    /// Classe che descrive il Giocatore, con funzioni e utilities.
    /// </summary>
    public class Player : ICloneable
    {
        /// <summary>
        /// ID del giocatore nel server. Viene usato per riconoscerlo.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; }

        /// <summary>
        /// Specifica se il player è online o no.
        /// </summary>
        public bool IsOnline { get; }

        /// <summary>
        /// Numero di carte del giocatore.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CardsNum { get; }

        /// <summary>
        /// Nome del giocatore.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Personalizzazioni del giocatore.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Personalization Personalizations { get; }

        /// <summary>
        /// Constructor del giocatore, usato anche durante la deserializzazione da JSON.
        /// </summary>
        /// <param name="id">ID del giocatore nel server</param>
        /// <param name="name">Username del giocatore</param>
        /// <param name="personalizations">Personalizzazioni</param>
        [JsonConstructor]
        public Player(uint? id, int? cardsNum, bool isOnline, string name, Personalization personalizations)
        {
            Id = id;
            Name = name;
            IsOnline = isOnline;
            CardsNum = cardsNum;
            if (personalizations != null)
                Personalizations = personalizations;
            else
                Personalizations = new Personalization();
        }

        /// <summary>
        /// Ritorna il label del giocatore con l'immagine profilo.
        /// </summary>
        /// <param name="isFocused">Ingrandisce il label, usato quando è il turno di un giocatore</param>
        /// <returns></returns>
        public Label GetAsLabel(bool isFocused)
        {
            var label = new Label();
            // TODO: Aggiungere immagine profilo
            label.AutoSize = true;
            label.Text = Name;
            if (isFocused)
                label.Font = new Font("Microsoft Sans Serif Bold", 20F);
            else
                label.Font = new Font("Microsoft Sans Serif", 15F);
            label.ForeColor = Personalizations.UsernameColor;
            label.BackColor = Personalizations.BackgroundColor;
            return label;
        }

        public object Clone() => new Player(Id, CardsNum, IsOnline, (string)Name.Clone(), (Personalization)Personalizations.Clone());
    }
}
