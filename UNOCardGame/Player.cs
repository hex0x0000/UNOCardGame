using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    /// <summary>
    /// Rappresenta un colore che può essere serializzato e mandato.
    /// Contiene tutte le funzioni per interfacciarsi con Color
    /// </summary>
    public class PlayerColor : ICloneable, IEquatable<PlayerColor>, IEquatable<Color>
    {
        /// <summary>
        /// Rosso
        /// </summary>
        public byte R { get; }

        /// <summary>
        /// Verde
        /// </summary>
        public byte G { get; }

        /// <summary>
        /// Blu
        /// </summary>
        public byte B { get; }

        /// <summary>
        /// Opacità (alpha)
        /// </summary>
        public byte A { get; }

        /// <summary>
        /// Costruisce PlayerColor partendo da Color
        /// </summary>
        /// <param name="color"></param>
        public PlayerColor(Color color) { R = color.R; G = color.G; B = color.B; A = color.A; }

        [JsonConstructor]
        public PlayerColor(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }

        public Color ToColor() => Color.FromArgb(R, G, B, A);

        public object Clone() => new PlayerColor(R, G, B, A);

        public bool Equals(PlayerColor other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    }

    /// <summary>
    /// Personalizzazioni del giocatore.
    /// </summary>
    public class Personalization : ICloneable
    {
        /// <summary>
        /// Colori possibili per il background dei giocatori.
        /// </summary>
        [JsonIgnore]
        public static readonly List<PlayerColor> BG_COLORS = new() { new(Color.White), new(Color.Beige), new(Color.DimGray) };

        /// <summary>
        /// Colori possibili per l'username dei giocatori.
        /// </summary>
        [JsonIgnore]
        public static readonly List<PlayerColor> USERNAME_COLORS = new() { new(Color.Black), new(Color.DeepSkyBlue), new(Color.DarkRed) };

        private PlayerColor _UsernameColor;

        /// <summary>
        /// Colore dell'username.
        /// </summary>
        public PlayerColor UsernameColor
        {
            get => _UsernameColor;
            private set
            {
                if (!USERNAME_COLORS.Contains(value))
                    throw new ArgumentException("Value must be contained within its possible values specified in USERNAME_COLORS", nameof(UsernameColor));
                _UsernameColor = value;
            }
        }

        private PlayerColor _BackgroundColor;

        /// <summary>
        /// Colore del background dell'utente.
        /// </summary>
        public PlayerColor BackgroundColor
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
        /// Inizializza una nuova personalizzazione
        /// </summary>
        /// <param name="usernameColor">Colore dell'username</param>
        /// <param name="backgroundColor">Colore del background dell'username</param>
        /// <param name="avatarImage">Nome del file dell'immagine profilo (senza estensione)</param>
        [JsonConstructor]
        public Personalization(PlayerColor usernameColor, PlayerColor backgroundColor)
        {
            UsernameColor = usernameColor;
            BackgroundColor = backgroundColor;
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
        }

        public object Clone() => new Personalization(UsernameColor, BackgroundColor);
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? IsOnline { get; set; }

        /// <summary>
        /// Numero di carte del giocatore.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? CardsNum { get; set; }

        /// <summary>
        /// Nome del giocatore.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Personalizzazioni del giocatore.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Personalization Personalizations { get; }

        public Player(string name)
        {
            Name = name;
            Personalizations = new Personalization();
        }

        /// <summary>
        /// Constructor del giocatore, usato anche durante la deserializzazione da JSON.
        /// </summary>
        /// <param name="id">ID del giocatore nel server</param>
        /// <param name="name">Username del giocatore</param>
        /// <param name="personalizations">Personalizzazioni</param>
        [JsonConstructor]
        public Player(uint? id, int? cardsNum, bool? isOnline, string name, Personalization personalizations)
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
        /// Ritorna il label del giocatore.
        /// </summary>
        /// <param name="isFocused">Ingrandisce il label, usato quando è il turno di un giocatore</param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        public Label GetAsLabel(bool isFocused)
        {
            ToolTip toolTip = new()
            {
                IsBalloon = true
            };

            Label label = new()
            {
                AutoSize = true,
                Text = Name,
                Font = (isFocused) ? new Font("Microsoft Sans Serif Bold", 20F) : new Font("Microsoft Sans Serif", 15F),
                ForeColor = Personalizations.UsernameColor.ToColor(),
                BackColor = Personalizations.BackgroundColor.ToColor(),
            };
            // Imposta un tooltip che mostra il numero di carte di questo giocatore
            toolTip.SetToolTip(label, $"{Name} ha {CardsNum} carte");
            return label;
        }

        public object Clone() => new Player(Id, CardsNum, IsOnline, (string)Name.Clone(), (Personalization)Personalizations.Clone());

        public override string ToString()
        {
            if (IsOnline is bool isOnline)
                if (!isOnline)
                    return $"{Name} (Offline)";
            return Name;
        }
    }
}
