using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace MrSprite
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            updateImage();
            updateCanvas();
        }

        private Point getPoint(int idx)
        {
            if (idx < 0)
                return new Point(0, 0);

            if (idx >= _points.Count)
                return new Point(canvas.ActualWidth, canvas.ActualHeight);

            return _points[idx].Pos;
        }

        private Point rescalePoint(Point pt)
        {
            return new Point(pt.X * canvas.ActualWidth, pt.Y * canvas.ActualHeight);
        }

        private void updateCanvas()
        {
            canvas.Children.Clear();

            for (int i = 0; i < _points.Count-1; ++i) {
                var lg = new LineGeometry() { StartPoint = _points[i].Pos, EndPoint = _points[i+1].Pos };
                canvas.Children.Add(new Path() { Stroke = Brushes.Black, StrokeThickness = 1, Data = lg, SnapsToDevicePixels = true });
            }
/*
            do {
                var lg = new LineGeometry() { StartPoint = getPoint(i - 1), EndPoint = getPoint(i) };
                canvas.Children.Add(new Path() { Stroke = Brushes.Black, StrokeThickness = 1, Data = lg, SnapsToDevicePixels = true });
            } while (++i < _points.Count + 1);
            */
            for (int i = 0; i < _points.Count; ++i ) {
                var cur = _points[i];
                var rg = new RectangleGeometry() { Rect = cur.boundingRect() };
                canvas.Children.Add(new Path() { Stroke = cur.Selected ? Brushes.LightBlue : Brushes.DarkRed, StrokeThickness = 1, Data = rg, SnapsToDevicePixels = true });
            }

            canvas.InvalidateVisual();
        }

        private void updateImage()
        {
            int width = 256;
            int height = 256;
            byte[] data = new byte[4 * width * height];

            float[] mapper = new float[2 * _points.Count];
            for (int i = 0; i < _points.Count; ++i) {
                var pt = _points[i].Pos; // getPoint(i);
                mapper[i * 2 + 0] = (float)(pt.X / canvas.ActualWidth);
                mapper[i * 2 + 1] = (float)(pt.Y / canvas.ActualHeight);
            }

            var pinnedMapper = GCHandle.Alloc(mapper, GCHandleType.Pinned);
            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            NativeMethods.create_bitmap(pinnedArray.AddrOfPinnedObject(), width, height, 32, pinnedMapper.AddrOfPinnedObject(), _points.Count * 2);
            PixelFormat format = PixelFormats.Bgr32;
            int stride = (width * format.BitsPerPixel + 7) / 8;
            bmpSource = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, data, stride);
            pinnedArray.Free();
            pinnedMapper.Free();
            img.Source = bmpSource;
        }

        private void canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            moving = false;
            movingPoint = null;

            var pp = Mouse.GetPosition(canvas);

            // don't allow multiple points on top of each other
            foreach (var p in _points) {
                if (p.boundingRect().Contains(pp))
                    return;
            }

            _points.Add(new ControlPoint() { Pos = new Point(pp.X, pp.Y) });
            _points.Sort((a, b) => a.Pos.X.CompareTo(b.Pos.X));

            updateImage();
            updateCanvas();
        }

        private void canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!moving)
                return;

            var pp = Mouse.GetPosition(canvas);

            movingPoint.Pos += pp - lastMovePos;
            movingPoint.Pos = new Point(
                Math.Max(0, Math.Min(canvas.ActualWidth, movingPoint.Pos.X)), 
                Math.Max(0, Math.Min(canvas.ActualHeight, movingPoint.Pos.Y)));
            lastMovePos = pp;

            updateImage();
            updateCanvas();
        }

        private void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            foreach (var p in _points)
                p.Selected = false;

            bool changed = false;
            var pp = Mouse.GetPosition(canvas);
            foreach (var p in _points) {
                if (p.boundingRect().Contains(pp)) {
                    p.Selected = true;
                    movingPoint = p;
                    changed = true;
                    break;
                }
            }

            if (changed) {
                updateCanvas();
                moving = true;
                startingMovePos = lastMovePos = Mouse.GetPosition(canvas);
            }
        }


        private void reset_Click(object sender, RoutedEventArgs e)
        {
            _points.Clear();
            updateImage();
            updateCanvas();
        }

        private void save_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Particle";
            dlg.DefaultExt = ".bmp";
            dlg.Filter = "Bmp files (.bmp)|*.bmp";

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true) {
                using (var s = new System.IO.FileStream(dlg.FileName, System.IO.FileMode.Create)) {
                    var encoder = new BmpBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmpSource));
                    encoder.Save(s);
                }
            }

        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key) {
                case Key.Delete:
                    foreach (var p in _points) {
                        if (p.Selected) {
                            _points.Remove(p);
                            movingPoint = null;
                            moving = false;
                            break;
                        }
                    }
                    break;

                case Key.Escape:
                    foreach (var p in _points)
                        p.Selected = false;
                    movingPoint = null;
                    moving = false;
                    break;
            }

            updateImage();
            updateCanvas();
        }

        class ControlPoint
        {
            public ControlPoint()
            {
                Selected = false;
            }

            public Point Pos { get; set; }
            public Rect boundingRect()
            {
                return new Rect() { X = Pos.X - rectSize, Y = Pos.Y - rectSize, Width = 2 * rectSize, Height = 2 * rectSize };
            }

            public bool Selected { get; set;  }
            private float rectSize = 5;
        }




        Point lastMovePos;
        Point startingMovePos;
        ControlPoint movingPoint = null;
        bool moving = false;
        BitmapSource bmpSource = null;
        List<ControlPoint> _points = new List<ControlPoint>();


    }

    internal static class NativeMethods
    {
        [DllImport("Worker.dll")]
        public static extern IntPtr create_bitmap(IntPtr ptr, int width, int height, int stride, IntPtr mapping, int mapping_count);
    }

}
