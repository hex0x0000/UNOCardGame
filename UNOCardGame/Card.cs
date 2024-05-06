using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text.Json;

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
    enum Colors {
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
    enum Specials {
        PLUS_FOUR,
        CHANGE_COLOR
    }

    /// <summary>
    /// Classe della Carta.
    /// Contiene tutte le informazioni della carta e le utilities.
    /// </summary>
    class Card
    {
        private Type type;
        private Colors? color = null;
        private Normals? normalType = null;
        private Specials? specialType = null;

        public Card(Colors color, Normals normalType)
        {
            type = Type.NORMAL;
            this.normalType = normalType;
            this.color = color;
        }

        public Card(Specials specialType)
        {
            type = Type.SPECIAL;
            this.specialType = specialType;
        }

        public static Card Deserialize(string json)
        {
            //try
            //{
                return JsonSerializer.Deserialize<Card>(json);
            //} catch (Exception e) { }
        }

        public string Serialize()
        {
            return JsonSerializer.Serialize(this);
        }

        public Button GetAsButton(Func<Card, int> fn)
        {
            Button btn = new Button();
            //btn.Image = new Image();
            btn.Click += (args, events) => fn(this);
            return btn;
        }
    }
}
