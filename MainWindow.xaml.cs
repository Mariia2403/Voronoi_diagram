using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        int[] pixelCounts;
        bool currentThreadMode = false;

        enum DistanceMetric { Euclidean, Manhattan, Chebyshev }
        DistanceMetric selectedMetric = DistanceMetric.Euclidean;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            width = (int)DrawCanvas.ActualWidth;
            height = (int)DrawCanvas.ActualHeight;

            //bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            var img = new System.Windows.Controls.Image { Source = bitmap };
            DrawCanvas.Children.Insert(0, img); // Додаємо Image НАЙПЕРШИМ, щоб він був "на дні"
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
                Canvas.SetLeft(el, p.X - 5);
                Canvas.SetTop(el, p.Y - 5);
                DrawCanvas.Children.Add(el);
            }
        }

        private void GeneratePoints_Click(object sender, RoutedEventArgs e)
        {
            double canvasWidth = DrawCanvas.ActualWidth;
            double canvasHeight = DrawCanvas.ActualHeight;

            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                MessageBox.Show("Полотно ще не промальоване — спробуйте трохи пізніше");
                return;
            }

           
            if (!int.TryParse(PointCountBox.Text, out int count) || count <= 0)
            {
                MessageBox.Show("Введіть коректну кількість точок (ціле число > 0)");
                return;
            }

            var rand = new Random();
            points.Clear();
            int margin = 5;

            for (int i = 0; i < count; i++)
            {
                points.Add(new Point(
                    rand.Next(margin, (int)(canvasWidth - margin)),
                    rand.Next(margin, (int)(canvasHeight - margin))
                ));
            }

            DrawPoints();
        }

        private void SingleThread_Click(object sender, RoutedEventArgs e)
        {
            currentThreadMode = false;
            RenderVoronoi(false);
        }

        private void MultiThread_Click(object sender, RoutedEventArgs e)
        {
            currentThreadMode = true;
            RenderVoronoi(true);
        }
        private void RenderVoronoi(bool multiThreaded)
        {
            width = (int)DrawCanvas.ActualWidth;
            height = (int)DrawCanvas.ActualHeight;

            if (points.Count == 0 || width <= 0 || height <= 0)
            {
                MessageBox.Show("Полотно ще не готове або немає точок.");
                return;
            }

            // Створити новий bitmap під розміри полотна
            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            if (DrawCanvas.Children.OfType<Image>().FirstOrDefault() is Image img)
            {
                img.Source = bitmap;
            }

            var stopwatch = Stopwatch.StartNew();
            var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;

            StatusText.Text = "Обчислюємо...";
            int stride = bitmap.BackBufferStride;
            int bytesPerPixel = 4;
            byte[] pixels = new byte[height * stride];

            pixelCounts = new int[points.Count];

            Action<int, int> renderSlice = (yStart, yEnd) =>
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int closestIndex = 0;
                        double minDist = Distance(x, y, points[0]);
                        for (int i = 1; i < points.Count; i++)
                        {
                            double dist = Distance(x, y, points[i]);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestIndex = i;
                            }
                        }

                        pixelCounts[closestIndex]++;
                        Color c = GetColorForIndex(closestIndex);
                        int index = y * stride + x * bytesPerPixel;
                        pixels[index] = c.B;
                        pixels[index + 1] = c.G;
                        pixels[index + 2] = c.R;
                        pixels[index + 3] = 255;
                    }
                }
            };

            if (!multiThreaded)
            {
                renderSlice(0, height);
            }
            else
            {
                int cores = Environment.ProcessorCount;
                int slice = height / cores;
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < cores; i++)
                {
                    int y0 = i * slice;
                    int y1 = (i == cores - 1) ? height : y0 + slice;
                    tasks.Add(Task.Run(() => renderSlice(y0, y1)));
                }

                Task.WaitAll(tasks.ToArray());
            }

            // Малюємо чорні точки поверх
            foreach (var p in points)
            {
                int px = (int)p.X;
                int py = (int)p.Y;

                for (int dx = -2; dx <= 2; dx++)
                {
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int x = px + dx;
                        int y = py + dy;

                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            int index = y * stride + x * bytesPerPixel;
                            pixels[index] = 0;       // B
                            pixels[index + 1] = 0;   // G
                            pixels[index + 2] = 0;   // R
                            pixels[index + 3] = 255; // A
                        }
                    }
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);

            stopwatch.Stop();
            var cpuEnd = Process.GetCurrentProcess().TotalProcessorTime;
            var memoryUsed = GC.GetTotalMemory(false);

            StatusText.Text = $"Реальний час: {stopwatch.Elapsed.TotalSeconds:F2} сек\n" +
                              $"CPU час: {(cpuEnd - cpuStart).TotalSeconds:F2} сек\n" +
                              $"Пам’ять: {memoryUsed / 1024 / 1024} МБ\n" +
                              $"Точок: {points.Count}, Пікселів: {width * height}";
        }

        private double Distance(int x, int y, Point p)
        {
            double dx = x - p.X, dy = y - p.Y;


            switch (selectedMetric)
            {
                case DistanceMetric.Euclidean:
                    return dx * dx + dy * dy;

                case DistanceMetric.Manhattan:
                    return Math.Abs(dx) + Math.Abs(dy);

                case DistanceMetric.Chebyshev:
                    return Math.Max(Math.Abs(dx), Math.Abs(dy));

                default:
                    throw new NotSupportedException("Обрана метрика не підтримується.");
            }
        }



        private Color GetColorForIndex(int index)
        {
            var r = (byte)(31 * index % 256);
            var g = (byte)(67 * index % 256);
            var b = (byte)(123 * index % 256);


            if (r == 0 && g == 0 && b == 0)
            {
                r = 50; g = 50; b = 50;
            }

            return Color.FromRgb(r, g, b);
        }

        private void RemoveWeakPoints_Click(object sender, RoutedEventArgs e)
        {
            if (points.Count == 0 || pixelCounts == null || pixelCounts.Length != points.Count)
            {
                MessageBox.Show("Спочатку побудуй діаграму!");
                return;
            }

            double removePercent = 0.2; // 20%
            int numToRemove = (int)(points.Count * removePercent);

            if (numToRemove == 0) return;

            var indicesToRemove = pixelCounts
                .Select((count, index) => new { count, index })
                .OrderBy(x => x.count)
                .Take(numToRemove)
                .Select(x => x.index)
                .OrderByDescending(i => i) // Видаляємо з кінця, щоб не зламати індекси
                .ToList();

            foreach (var i in indicesToRemove)
            {
                points.RemoveAt(i);
            }

            DrawPoints();
            RenderVoronoi(currentThreadMode); // Поточний режим — однопотоковий чи багатопотоковий
        }

        private void MetricSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MetricSelector.SelectedItem is ComboBoxItem selectedItem)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Євклідова":
                        selectedMetric = DistanceMetric.Euclidean;
                        break;
                    case "Манхеттенська":
                        selectedMetric = DistanceMetric.Manhattan;
                        break;
                    case "Чебишева":
                        selectedMetric = DistanceMetric.Chebyshev;
                        break;
                }

                RenderVoronoi(currentThreadMode); // Перемалювати!
            }
        }
    }
}

