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
    public partial class MainGame : Form
    {
        private Client client;

        public MainGame(Player player, string address, ushort port, bool isDNS)
        {
            InitializeComponent();
            client = new Client(player, address, port, isDNS);
        }

        private void Interface_Load(object sender, EventArgs e)
        {
            
        }
    }
}
