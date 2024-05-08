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
    class Personalization
    {
        /// <summary>
        /// Colori possibili per il background dei giocatori.
        /// </summary>
        public static readonly List<Color> BG_COLORS = new List<Color>() { Color.White, Color.Beige, Color.DimGray };

        /// <summary>
        /// Colori possibili per l'username dei giocatori.
        /// </summary>
        public static readonly List<Color> USERNAME_COLORS = new List<Color> { Color.Black, Color.DeepSkyBlue, Color.DarkRed };

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

        /// <summary>
        /// Colore dell'username.
        /// </summary>
        public Color UsernameColor { get; }

        /// <summary>
        /// Colore del background dell'utente.
        /// </summary>
        public Color BackgroundColor { get; }

        /// <summary>
        /// Nome del file dell'immagine profilo.
        /// </summary>
        public string AvatarImage { get; }

        /// <summary>
        /// Deserializza il JSON della personalizzazione e la trasforma nella classe.
        /// </summary>
        /// <param name="json">JSON da deserializzare</param>
        /// <returns>Classe Personalization letta dal JSON</returns>
        public static Personalization Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Personalization>(json);
        }

        /// <summary>
        /// Serializza la classe e la trasforma in JSON.
        /// </summary>
        /// <returns>Stringa contenente la personalizzazione serializzata</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// Classe che descrive il Giocatore, con funzioni e utilities.
    /// </summary>
    internal class Player
    {
        /// <summary>
        /// ID del giocatore per il server. Viene usato per riconoscerlo.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Nome del giocatore.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Personalizzazioni del giocatore.
        /// </summary>
        public Personalization Personalizations { get; }

        /// <summary>
        /// Constructor del giocatore, usato anche durante la deserializzazione da JSON.
        /// </summary>
        /// <param name="id">ID del giocatore nel server</param>
        /// <param name="name">Username del giocatore</param>
        /// <param name="personalizations">Personalizzazioni</param>
        [JsonConstructor]
        public Player(int id, string name, Personalization personalizations)
        {
            Id = id;
            Name = name;
            if (personalizations != null)
            {
                Personalizations = personalizations;
            }
            else
            {
                Personalizations = new Personalization();
            }
        }

        /// <summary>
        /// Deserializza il JSON del giocatore e lo trasforma nella classe.
        /// </summary>
        /// <param name="json">JSON da deserializzare</param>
        /// <returns>Classe player letta dal JSON</returns>
        public static Player Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Player>(json);
        }

        /// <summary>
        /// Serializza la classe e la trasforma in JSON.
        /// </summary>
        /// <returns>Stringa contenente il player serializzato</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Ritorna il label del giocatore con l'immagine profilo.
        /// </summary>
        /// <param name="isFocused">Ingrandisce il label, usato quando è il turno di un giocatore</param>
        /// <returns></returns>
        public Label GetAsLabel(bool isFocused)
        {
            Label label = new Label();
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
    }
}
