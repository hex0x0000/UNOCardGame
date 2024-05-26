using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UNOCardGame
{
    [SupportedOSPlatform("windows")]
    public partial class ColorSelection : Form
    {
        private Colors Result;

        public static Colors SelectColor()
        {
            var form = new ColorSelection();
            form.Show();
            return form.Result;
        }

        private ColorSelection()
        {
            InitializeComponent();
        }

        private void redButton_Click(object sender, EventArgs e)
        {
            Result = Colors.Red;
            Close();
        }

        private void blueButton_Click(object sender, EventArgs e)
        {
            Result = Colors.Blue;
            Close();
        }

        private void yellowButton_Click(object sender, EventArgs e)
        {
            Result = Colors.Yellow;
            Close();
        }

        private void greenButton_Click(object sender, EventArgs e)
        {
            Result = Colors.Green;
            Close();
        }
    }
}
