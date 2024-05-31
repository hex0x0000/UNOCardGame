using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
        private Colors? Result = null;

        public static Colors? SelectColor()
        {
            var form = new ColorSelection();
            form.ShowDialog();
            return form.Result;
        }

        private ColorSelection()
        {
            InitializeComponent();
            try
            {
                redButton.Image = (Image)Properties.Resources.ResourceManager.GetObject("Red");
                blueButton.Image = (Image)Properties.Resources.ResourceManager.GetObject("Blue");
                yellowButton.Image = (Image)Properties.Resources.ResourceManager.GetObject("Yellow");
                greenButton.Image = (Image)Properties.Resources.ResourceManager.GetObject("Green");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Impossibile caricare i colori nel ColorSelection: {ex}");
            }
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Result == null)
            {
                MessageBox.Show("Devi selezionare un colore.");
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
