using System.Windows.Controls;

namespace NetworkService.Views
{
    public partial class GraphView : UserControl
    {
        public GraphView()
        {
            InitializeComponent();
        }

        private void BarCanvas_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (DataContext is ViewModel.GraphViewModel vm)
            {
                vm.CanvasWidth = e.NewSize.Width;
                vm.CanvasHeight = e.NewSize.Height;
                vm.RedrawGraph();
            }
        }
    }
}