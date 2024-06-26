﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

        public Color ToColor() => Color.FromArgb(A, R, G, B);

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
        public static readonly List<PlayerColor> BG_COLORS = [new(Color.White), new(Color.Beige), new(Color.DimGray)];

        /// <summary>
        /// Colori possibili per l'username dei giocatori.
        /// </summary>
        [JsonIgnore]
        public static readonly List<PlayerColor> USERNAME_COLORS = [new(Color.Black), new(Color.Chocolate), new(Color.DarkRed)];

        /// <summary>
        /// Colore dell'username.
        /// </summary>
        public PlayerColor UsernameColor { get; set; }
    
        /// <summary>
        /// Colore del background dell'utente.
        /// </summary>
        public PlayerColor BackgroundColor { get; set; }

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
        const int NAME_MAX_CHARS = 8;

        /// <summary>
        /// ID del giocatore nel server. Viene usato per riconoscerlo.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public uint? Id { get; }

        /// <summary>
        /// Specifica se il player è online o no.
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// Specifica il posto in classifica se il player ha vinto
        /// Altrimenti null
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Won { get; set; } = null;

        /// <summary>
        /// Taglia il nome del player per farlo stare in NAME_MAX_CHARS caratteri.
        /// </summary>
        private static string NameCut(string value) => value.Length <= NAME_MAX_CHARS ? value : value[..NAME_MAX_CHARS];

        /// <summary>
        /// Lettere non alfanumeriche
        /// </summary>
        private static readonly Regex _NonAlphaNum = new("[^a-zA-Z0-9]", RegexOptions.Compiled);

        private string _Name;

        /// <summary>
        /// Nome del giocatore.
        /// </summary>
        public string Name
        {
            get => _Name; private set
            {
                if (value == "") throw new ArgumentException("Devi inserire il nome del player.");
                _Name = NameCut(_NonAlphaNum.Replace(value, ""));
            }
        }

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

        public Player(string name, Personalization personalizations)
        {
            Name = name;
            Personalizations = personalizations;
        }

        /// <summary>
        /// Constructor del giocatore, usato anche durante la deserializzazione da JSON.
        /// </summary>
        /// <param name="id">ID del giocatore nel server</param>
        /// <param name="name">Username del giocatore</param>
        /// <param name="personalizations">Personalizzazioni</param>
        [JsonConstructor]
        public Player(uint? id, bool isOnline, string name, Personalization personalizations, int? won)
        {
            Id = id;
            Name = name;
            IsOnline = isOnline;
            Won = won;
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
        public Label GetAsLabel(bool isFocused, int cardsNum)
        {
            Label label = new()
            {
                AutoSize = true,
                Text = ToString(),
                Font = (isFocused) ? new Font("Microsoft Sans Serif Bold", 20F) : new Font("Microsoft Sans Serif", 15F),
                ForeColor = Personalizations.UsernameColor.ToColor(),
                BackColor = Personalizations.BackgroundColor.ToColor(),
            };
            // Imposta un tooltip che mostra il numero di carte di questo giocatore
            if (cardsNum > 0)
            {
                ToolTip toolTip = new()
                {
                    IsBalloon = true
                };
                toolTip.SetToolTip(label, $"{Name} ha {cardsNum} carte");
            }
            return label;
        }

        public object Clone() => new Player(Id, IsOnline, (string)Name.Clone(), (Personalization)Personalizations.Clone(), Won);

        public override string ToString()
        {
            string toStr = Name;
            if (!IsOnline)
                toStr += " (Offline)";
            if (Won != null)
                toStr += " (Ha vinto)";
            return toStr;
        }
    }
}
