using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UNOCardGame
{
    /// <summary>
    /// Tipo della carta
    /// </summary>
    enum Type
    {
        NORMAL,
        SPECIAL
    }

    /// <summary>
    /// Colore delle carte normali
    /// </summary>
    enum Colors
    {
        RED,
        GREEN,
        BLUE,
        YELLOW
    }

    /// <summary>
    /// Carte normali (con un colore)
    /// </summary>
    enum Normals
    {
        ONE,
        TWO,
        THREE,
        FOUR,
        FIVE,
        SIX,
        SEVEN,
        EIGHT,
        NINE,
        PLUS_TWO,
        REVERSE,
        BLOCK
    }

    /// <summary>
    /// Carte speciali
    /// </summary>
    enum Specials
    {
        PLUS_FOUR,
        CHANGE_COLOR
    }

    /// <summary>
    /// Classe della Carta.
    /// Contiene tutte le informazioni della carta e le sue utilities.
    /// </summary>
    class Card
    {
        private Type type;
        private Colors? color = null;
        private Normals? normalType = null;
        private Specials? specialType = null;

        /// <summary>
        /// L'ID della carta del server. Viene usato dal server per riconoscere la carta
        /// </summary>
        public int? Id { get; }

        /// <summary>
        /// Il tipo della carta, se normale o speciale
        /// </summary>
        public Type Type => type;

        /// <summary>
        /// Il colore della carta, se è una carta normale
        /// </summary>
        public Colors? Color => color;

        /// <summary>
        /// Il tipo della carta, se è una carta normale
        /// </summary>
        public Normals? NormalType => normalType;

        /// <summary>
        /// Il tipo della carta, se è una carta speciale
        /// </summary>
        public Specials? SpecialType => specialType;

        /// <summary>
        /// Inizializza la carta come tipo normale (carta con un colore).
        /// </summary>
        /// <param name="normalType">Il tipo della carta (uno, due, tre, ..., cambio giro, blocca...)</param>
        /// <param name="color">Il colore della carta</param>
        public Card(Normals normalType, Colors color)
        {
            type = Type.NORMAL;
            this.normalType = normalType;
            this.color = color;
        }

        /// <summary>
        /// Inizializza la carta come carta speciale (senza colore, capace di cambiare il colore nella partita).
        /// </summary>
        /// <param name="specialType">Tipo di carta</param>
        public Card(Specials specialType)
        {
            type = Type.SPECIAL;
            this.specialType = specialType;
        }

        /// <summary>
        /// Constructor usato per deserializzare da JSON.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="color"></param>
        /// <param name="normalType"></param>
        /// <param name="specialType"></param>
        /// <param name="id"></param>
        [JsonConstructor]
        public Card(Type type, Colors? color, Normals? normalType, Specials? specialType, int? id)
        {
            this.type = type; this.color = color; this.normalType = normalType; this.specialType = specialType; this.Id = id;
        }

        /// <summary>
        /// Deserializza la carta da JSON.
        /// </summary>
        /// <param name="json">Il JSON da deserializzare</param>
        /// <returns>Ritorna la carta deserializzata</returns>
        public static Card Deserialize(string json)
        {
            return JsonSerializer.Deserialize<Card>(json);
        }

        /// <summary>
        /// Serializza la carta e la trasforma in JSON.
        /// </summary>
        /// <returns>La carta in JSON</returns>
        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Ritorna la carta come bottone
        /// </summary>
        /// <param name="fn">L'azione del bottone quando cliccato</param>
        /// <returns>Bottone</returns>
        public Button GetAsButton(Func<Card, int> fn)
        {
            Button btn = new Button();
            //btn.Image = new Image();
            btn.Text = ToString();
            btn.Size = new System.Drawing.Size(150, 250);
            btn.Click += (args, events) => fn(this);
            return btn;
        }

        public override string ToString()
        {
            switch (type)
            {
                case Type.NORMAL:
                    return $"{normalType}-{color}";
                case Type.SPECIAL:
                    return specialType.ToString();
                default:
                    return "";
            }
        }
    }
}
