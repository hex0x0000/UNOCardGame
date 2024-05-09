using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    public partial class Interface : Form
    {
        public Interface()
        {
            InitializeComponent();
        }

        private void Interface_Load(object sender, EventArgs e)
        {
            // Test serializzazione/deserializzazione e UI
            Func<Card, int> fn = (card) => { Console.WriteLine(card.Serialize()); return 0; };
            Card testCard = new Card(Specials.CHANGE_COLOR);
            Card testCard2 = new Card(Normals.EIGHT, Colors.GREEN);
            Player player = new Player(0, "Test", null);
            Console.WriteLine(Card.Deserialize(testCard.Serialize()));
            Console.WriteLine(testCard2.Serialize());
            cards.Controls.Add(testCard.GetAsButton(fn));
            cards.Controls.Add(testCard2.GetAsButton(fn));
            cards.Controls.Add(Card.Deserialize(testCard.Serialize()).GetAsButton(fn));
            players.Controls.Add(player.GetAsLabel(true));

            // Test Card.PickRandom
            var dict = new Dictionary<String, int>();
            int i;
            for (i = 0; i < 1000000; i++)
            {
                var card = Card.PickRandom().ToString();
                if (dict.ContainsKey(card))
                    dict[card]++;
                else
                    dict.Add(card, 1);
            }
            // I valori devono approssimarsi al loro numero di carte relativo nel mazzo
            foreach (KeyValuePair<string, int> entry in dict)
                Console.WriteLine($"{entry.Key}: {(entry.Value / (double)i) * 108}"); // Funziona!

            // Test dei setter
            try
            {
                var testCard3 = new Card((Normals)(2), (Colors)3);
            }
            catch (Exception _e)
            {
                Console.WriteLine($"Caught: {_e}");
            }
        }
    }
}
