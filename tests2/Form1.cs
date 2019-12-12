using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tests2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        Bitmap b = new Bitmap(@"C:\Users\Techcraft7\Documents\coh\58381344_10219400930038527_8012584884545519616_n.jpg");
        private void Form1_Load(object sender, EventArgs e)
        {
            panel1.BackgroundImageLayout = ImageLayout.Stretch;
            panel1.BackgroundImage = ScaleImage(b, 1920, 1080);
            Console.WriteLine($"{panel1.BackgroundImage.Width}x{panel1.BackgroundImage.Height}");
        }

        public Bitmap ScaleImage(Bitmap bmp, int maxWidth, int maxHeight)
        {
            var ratioX = (double)maxWidth / bmp.Width;
            var ratioY = (double)maxHeight / bmp.Height;
            var ratio = Math.Min(ratioX, ratioY);
            var newWidth = (int)(bmp.Width * ratio);
            var newHeight = (int)(bmp.Height * ratio);
            var newImage = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(newImage))
            {
                graphics.DrawImage(bmp, 0, 0, newWidth, newHeight);
                bmp = new Bitmap(newWidth, newHeight, graphics);
            }
            return newImage;
        }
    }
}
