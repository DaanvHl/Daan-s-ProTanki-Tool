using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

namespace Automatization.UI.Coordinate
{
    public partial class CoordinatePickerWindow : Window
    {
        public Point SelectedPoint { get; private set; }

        public CoordinatePickerWindow()
        {
            InitializeComponent();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectedPoint = PointToScreen(e.GetPosition(this));
            DialogResult = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
        }
    }
}