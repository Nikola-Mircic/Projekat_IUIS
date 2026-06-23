using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using NetworkService.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NetworkService.View
{
    public partial class NetworkDisplayView : UserControl
    {
        // ── Drag state varijable (prema skripti) ──────────────────────
        private bool _isDragging = false;
        private ServerEntity _draggedEntity = null;
        private bool _isFromCanvas = false;
        private CanvasSlot _sourceSlot = null;

        public NetworkDisplayView()
        {
            InitializeComponent();
        }

        private NetworkDisplayViewModel Vm => DataContext as NetworkDisplayViewModel;

        // ── TreeView SelectedItemChanged — inicira drag (prema skripti) ──
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isDragging) return;
            if (e.NewValue is EntityGroup) return;

            if (e.NewValue is ServerEntity entity)
            {
                _isDragging = true;
                _draggedEntity = entity;
                _isFromCanvas = false;
                _sourceSlot = null;

                // Zatvaramo drawer pre nego što počnemo drag
                if (Vm != null && Vm.IsTreeViewOpen)
                    Vm.IsTreeViewOpen = false;

                DragDrop.DoDragDrop(
                    (TreeView)sender,
                    _draggedEntity,
                    DragDropEffects.Move);
            }
        }

        // ── TreeView MouseLeftButtonUp — resetuje drag state (prema skripti) ──
        private void TreeView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ResetDragState();
        }

        // ── Canvas ćelija MouseLeftButtonDown — drag između canvas ćelija ──
        private void EntitySlot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging) return;

            if (sender is Border border && border.Tag is CanvasSlot slot && slot.Entity != null)
            {
                _isDragging = true;
                _draggedEntity = slot.Entity;
                _isFromCanvas = true;
                _sourceSlot = slot;

                DragDrop.DoDragDrop(
                    border,
                    _draggedEntity,
                    DragDropEffects.Move);

                e.Handled = true;
            }
        }

        // ── Canvas MouseLeftButtonUp — resetuje drag state ───────────
        private void EntitySlot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ResetDragState();
        }

        // ── DragOver — vizuelna indikacija (prema skripti) ───────────
        private void CanvasSlot_DragOver(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.Tag is CanvasSlot slot)
            {
                // Prema skripti: proveravamo "taken" resurs — kod nas je slot.IsOccupied
                if (slot.IsOccupied && !_isFromCanvas)
                {
                    e.Effects = DragDropEffects.None;
                }
                else if (slot.IsOccupied && _isFromCanvas && slot == _sourceSlot)
                {
                    // Ne možemo spustiti na isti slot
                    e.Effects = DragDropEffects.None;
                }
                else if (slot.IsEmpty)
                {
                    e.Effects = DragDropEffects.Move;
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED));
                    border.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF0, 0xFF));
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }

            // Obavezno prema skripti — sprečava dalje propagiranje eventa
            e.Handled = true;
        }

        // ── Drop — spuštanje entiteta na slot (prema skripti) ────────
        private void CanvasSlot_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.Tag is CanvasSlot targetSlot)
            {
                // Proveravamo da li je draggedEntity validan — kao u skripti
                if (_draggedEntity != null)
                {
                    if (targetSlot.IsEmpty)
                    {
                        var dragData = new DragData(_draggedEntity, !_isFromCanvas, _sourceSlot);
                        Vm?.HandleDrop(dragData, targetSlot);
                    }
                }

                // Vraćamo normalan izgled ćelije
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
                border.Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB));

                ResetDragState();
            }

            // Obavezno prema skripti
            e.Handled = true;
        }

        // ── DragLeave — vraćamo normalan izgled ──────────────────────
        private void CanvasSlot_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border && border.Tag is CanvasSlot slot && slot.IsEmpty)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
                border.Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xFA, 0xFB));
            }
        }

        // ── Reset drag state (prema skripti — ResetDragState metoda) ─
        private void ResetDragState()
        {
            _isDragging = false;
            _draggedEntity = null;
            _isFromCanvas = false;
            _sourceSlot = null;
        }

        // ── Overlay klik zatvara drawer ───────────────────────────────
        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Vm?.ToggleTreeViewCommand.Execute(null);
        }
    }

    // ── Klasa za prenos podataka ──────────────────────────────────────
    public class DragData
    {
        public ServerEntity Entity { get; }
        public bool IsFromTree { get; }
        public CanvasSlot SourceSlot { get; }

        public DragData(ServerEntity entity, bool isFromTree, CanvasSlot sourceSlot = null)
        {
            Entity = entity;
            IsFromTree = isFromTree;
            SourceSlot = sourceSlot;
        }
    }
}