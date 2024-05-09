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
        NORMAL, // 100 carte su 108
        SPECIAL // 8 carte su 108
    }

    /// <summary>
    /// Colore delle carte normali.
    /// </summary>
    enum Colors
    {
        RED, // Stesse probabilità (1/4)
        GREEN,
        BLUE,
        YELLOW
    }

    /// <summary>
    /// Carte normali (con un colore), in totale 100.
    /// Per ogni tipo sono presenti due copie di carte,
    /// a parte per lo 0 che ne ha solo una.
    /// </summary>
    enum Normals
    {
        ZERO, // 4 carte su 100 (1/25 di probabilità)
        ONE, // 8 carte su 100 (2/25 di probabilità)
        TWO, // ...
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
        PLUS_FOUR, // Stesse probabilità (1/2)
        CHANGE_COLOR
    }

    /// <summary>
    /// Classe della Carta.
    /// Contiene tutte le informazioni della carta e le sue utilities.
    /// </summary>
    class Card : Serialization<Card>
    {
        /// <summary>
        /// L'ID della carta nel server.
        /// Viene usato dal server per riconoscere la carta nell'hashmap del server.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Id { get; }

        private Type _Type;

        /// <summary>
        /// Il tipo della carta, se normale o speciale
        /// </summary>
        public Type Type
        {
            get => _Type;
            private set
            {
                if ((int)value < 0 || (int)value >= _TypesEnumLength)
                    throw new ArgumentOutOfRangeException(nameof(Type), "Enum must stay within its range");
                _Type = value;
            }
        }

        private Colors? _Color;
        
        /// <summary>
        /// Il colore della carta, se è una carta normale
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Colors? Color {
            get => _Color;
            private set
            {
                if (value != null && ((int)value < 0 || (int)value >= _ColorsEnumLength))
                    throw new ArgumentOutOfRangeException(nameof(Colors), "Enum must stay within its range");
                _Color = value;
            }
        }

        private Normals? _NormalType;
        
        /// <summary>
        /// Il tipo della carta, se è una carta normale
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Normals? NormalType {
            get => _NormalType;
            private set
            {
                if (value != null && ((int)value < 0 || (int)value >= _NormalsEnumLength))
                    throw new ArgumentOutOfRangeException(nameof(Normals), "Enum must stay within its range");
                _NormalType = value;
            }
        }

        private Specials? _SpecialType;
        
        /// <summary>
        /// Il tipo della carta, se è una carta speciale
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Specials? SpecialType {
            get => _SpecialType;
            private set
            {
                if (value != null && ((int)value < 0 || (int)value >= _SpecialsEnumLength))
                    throw new ArgumentOutOfRangeException(nameof(Specials), "Enum must stay within its range");
                _SpecialType = value;
            }
        }

        /// <summary>
        /// Generatore di numeri casuali.
        /// </summary>
        private static Random _Random = new Random();

        /// <summary>
        /// Numero di elementi nell'enum dei colori.
        /// </summary>
        private static readonly int _TypesEnumLength = Enum.GetValues(typeof(UNOCardGame.Type)).Length;

        /// <summary>
        /// Numero di elementi nell'enum delle carte normali.
        /// </summary>
        private static readonly int _NormalsEnumLength = Enum.GetValues(typeof(Normals)).Length;

        /// <summary>
        /// Numero di elementi nell'enum delle carte speciali.
        /// </summary>
        private static readonly int _SpecialsEnumLength = Enum.GetValues(typeof(Specials)).Length;

        /// <summary>
        /// Numero di elementi nell'enum dei colori.
        /// </summary>
        private static readonly int _ColorsEnumLength = Enum.GetValues(typeof(Colors)).Length;

        /// <summary>
        /// Inizializza la carta come tipo normale (carta con un colore).
        /// </summary>
        /// <param name="normalType">Il tipo della carta (uno, due, tre, ..., cambio giro, blocca...)</param>
        /// <param name="color">Il colore della carta</param>
        public Card(Normals normalType, Colors color)
        {
            Type = Type.NORMAL;
            NormalType = normalType;
            Color = color;
        }

        /// <summary>
        /// Inizializza la carta come carta speciale (senza colore, capace di cambiare il colore nella partita).
        /// </summary>
        /// <param name="specialType">Tipo di carta</param>
        public Card(Specials specialType)
        {
            Type = Type.SPECIAL;
            SpecialType = specialType;
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
            Type = type; Color = color; NormalType = normalType; SpecialType = specialType; Id = id;
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

        /// <summary>
        /// Seleziona un valore casuale da uno degli enum che descrivono le carte.
        /// Ogni field dell'enum viene selezionato in base alla sua probabilità nel totale dell'enum.
        /// </summary>
        /// <typeparam name="T">Uno degli enum delle carte</typeparam>
        /// <returns></returns>
        private static int PickRandomEnum<T>() where T : Enum
        {
            double rand;
            switch (typeof(T).Name)
            {
                case nameof(UNOCardGame.Type):
                    // Le carte speciali sono 8 su 108, mentre quelle normali sono 100 su 108.
                    // Ci sono 8/108esimi di possibilità che rand sia minore del numero 8/108
                    // e 100/108esimi di possibilità che rand sia maggiore del numero 8/108
                    rand = _Random.NextDouble();
                    if (rand <= 8.0 / 108.0)
                        return (int)Type.SPECIAL;
                    else
                        return (int)Type.NORMAL;
                case nameof(Normals):
                    // Le carte normali sono 96 + 4 zeri, tutti i tipi hanno pari probabilità di venire
                    // estratti (2/25esimi) a parte gli zeri (1/25esimo).
                    // L'algoritmo controlla tutti i numeri da 1/25esimo a 25/25esimi (comparando rand e limit)
                    // e ci associa un tipo di carta (Zero, One, Two...).
                    rand = _Random.NextDouble();
                    double limit = 1.0 / 25.0;
                    if (rand <= limit)
                        return (int)Normals.ZERO;
                    for (int i = 1; i <= _NormalsEnumLength; i++, limit += 2.0 / 25.0)
                        if (rand <= limit)
                            return i - 1;
                    return -1;
                // Colors e Specials fra di loro hanno pari possibilità di venire estratti.
                case nameof(Colors):
                    return _Random.Next(_ColorsEnumLength);
                case nameof(Specials):
                    return _Random.Next(_SpecialsEnumLength);
                default:
                    // -1 causa un errore nel setter.
                    return -1;
            }
        }

        /// <summary>
        /// Estrae una carta randomica, seguendo le probabilità del mazzo.
        /// </summary>
        /// <returns>Una nuova carta selezionata casualmente</returns>
        public static Card PickRandom()
        {
            // Moltiplicando le varie probabilità si può ricavare
            // la probabilità della singola carta nel mazzo
            switch ((Type)PickRandomEnum<Type>())
            {
                case Type.NORMAL:
                    return new Card((Normals)PickRandomEnum<Normals>(), (Colors)PickRandomEnum<Colors>());
                case Type.SPECIAL:
                    return new Card((Specials)PickRandomEnum<Specials>());
                default:
                    return null;
            }
        }

        public override string ToString()
        {
            switch (Type)
            {
                case Type.NORMAL:
                    return $"{NormalType}-{Color}";
                case Type.SPECIAL:
                    return SpecialType.ToString();
                default:
                    return "";
            }
        }
    }
}
