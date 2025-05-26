using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfAppDiagramV
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        List<Point> points = new List<Point>();
        WriteableBitmap bitmap;
        int width, height;
        public MainWindow()
        {
            InitializeComponent();
            width = (int)this.Width;
            height = (int)this.Height;
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var img = new System.Windows.Controls.Image { Source = bitmap };
            DrawCanvas.Children.Add(img);
        }
        private void DrawCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(DrawCanvas);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                points.Add(pos);
            }
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                points.RemoveAll(p => (p - pos).Length < 5);
            }
            DrawPoints();
        }
        private void DrawPoints()
        {
            DrawCanvas.Children.OfType<Ellipse>().ToList().ForEach(e => DrawCanvas.Children.Remove(e));
            foreach (var p in points)
            {
                var el = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.Black
                };
                Canvas.SetLeft(el, p.X - 2.5);
                Canvas.SetTop(el, p.Y - 2.5);
                DrawCanvas.Children.Add(el);
            }
        }
    }
}
