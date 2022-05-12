using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace AutoScout24
{
    public partial class Form_Main : Form
    {
        private Scraper scraper = null;

        public Form_Main()
        {
            InitializeComponent();
            string cvvlist = "";
            for (int i = 0; i < 1000; i++)
            {
                string cvv = i.ToString("D3");
                cvvlist += cvv + "\r\n";
            }
            textBox2.Text = cvvlist;
        }

        private void Form_Main_Load(object sender, EventArgs e)
        {
        }

        private void button_StartPublshing_Click(object sender, EventArgs e)
        {
            if (scraper != null) scraper.Dispose();
            scraper = new Scraper();
            Console.WriteLine("Press Start button to start.");
            scraper.RunCvv(textBox2.Text, textBox1.Text);
            button_StartPublishing.Enabled = false;
        }

        private void Form_Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (scraper != null)
                scraper.Dispose();
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
    }
}
