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
            Func<Card, int> fn = (card) => { Console.WriteLine(card.Serialize()); return 0; };
            Card testCard = new Card(Specials.CHANGE_COLOR);
            Card testCard2 = new Card(Normals.EIGHT, Colors.GREEN);
            Player player = new Player(0, "Test", null);
            cards.Controls.Add(testCard.GetAsButton(fn));
            cards.Controls.Add(testCard2.GetAsButton(fn));
            cards.Controls.Add(Card.Deserialize(testCard.Serialize()).GetAsButton(fn));
            players.Controls.Add(player.GetAsLabel(true));
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
