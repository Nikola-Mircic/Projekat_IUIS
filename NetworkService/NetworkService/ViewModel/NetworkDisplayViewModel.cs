using NetworkService.Commands;
using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using NetworkService.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace NetworkService.ViewModel
{
    public class NetworkDisplayViewModel : BaseViewModel
    {
        private ObservableCollection<ServerEntity> Entities =>
            MainWindowViewModel.Entities;

        // ── Canvas ćelije (4x3 = 12) ─────────────────────────────────
        public ObservableCollection<CanvasSlot> CanvasSlots { get; }
            = new ObservableCollection<CanvasSlot>();

        // ── Entiteti grupisani po tipu (za TreeView) ──────────────────
        public ObservableCollection<EntityGroup> GroupedEntities { get; }
            = new ObservableCollection<EntityGroup>();

        // ── Linije između entiteta ────────────────────────────────────
        public ObservableCollection<ConnectionLine> Connections { get; }
            = new ObservableCollection<ConnectionLine>();

        // ── TreeView toggle ───────────────────────────────────────────
        private bool _isTreeViewOpen = true;
        public bool IsTreeViewOpen
        {
            get => _isTreeViewOpen;
            set
            {
                _isTreeViewOpen = value;
                OnPropertyChanged(nameof(IsTreeViewOpen));
                OnPropertyChanged(nameof(TreeToggleIcon));
            }
        }

        public string TreeToggleIcon => IsTreeViewOpen ? "»" : "«";

        // ── Undo ─────────────────────────────────────────────────────
        private readonly Stack<IUndoAction> _undoStack = new Stack<IUndoAction>();

        // ── Crtanje linija — state ────────────────────────────────────
        private CanvasSlot _lineStartSlot = null;

        // ── Komande ──────────────────────────────────────────────────
        public ICommand ToggleTreeViewCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand StartConnectionCommand { get; }

        public NetworkDisplayViewModel()
        {
            // Kreiramo 12 praznih ćelija
            for (int i = 0; i < 12; i++)
                CanvasSlots.Add(new CanvasSlot { Index = i });

            // Pratimo promene liste entiteta
            Entities.CollectionChanged += (s, e) => RefreshTreeView();
            RefreshTreeView();

            ToggleTreeViewCommand = new RelayCommand(_ => IsTreeViewOpen = !IsTreeViewOpen);
            UndoCommand = new RelayCommand(_ => ExecuteUndo(),
                                        _ => _undoStack.Count > 0);
            StartConnectionCommand = new RelayCommand(slot => HandleConnectionClick(slot as CanvasSlot));
        }

        // ── TreeView refresh ──────────────────────────────────────────
        private void RefreshTreeView()
        {
            // Entiteti koji NISU na canvas-u
            var placedEntities = CanvasSlots
                .Where(s => s.IsOccupied)
                .Select(s => s.Entity)
                .ToHashSet();

            var available = Entities.Where(e => !placedEntities.Contains(e));

            GroupedEntities.Clear();
            foreach (var group in available.GroupBy(e => e.Type?.Name ?? "Unknown"))
            {
                GroupedEntities.Add(new EntityGroup
                {
                    TypeName = group.Key,
                    Entities = new ObservableCollection<ServerEntity>(group)
                });
            }
        }

        // ── Drag&Drop logika ──────────────────────────────────────────
        public void HandleDrop(DragData dragData, CanvasSlot targetSlot)
        {
            if (targetSlot.IsOccupied) return;

            if (dragData.IsFromTree)
            {
                // Entitet dolazi iz TreeView-a
                targetSlot.Entity = dragData.Entity;
                _undoStack.Push(new DropFromTreeAction(targetSlot, dragData.Entity, this));
            }
            else
            {
                // Entitet se premešta između canvas ćelija
                var sourceSlot = dragData.SourceSlot;
                if (sourceSlot == null || sourceSlot == targetSlot) return;

                // Pomeramo linije
                UpdateConnectionsForMove(sourceSlot, targetSlot);

                targetSlot.Entity = dragData.Entity;
                sourceSlot.Entity = null;

                _undoStack.Push(new MoveOnCanvasAction(sourceSlot, targetSlot, dragData.Entity, this));
            }

            RefreshTreeView();
            RefreshConnections();
        }

        // ── Crtanje linija ────────────────────────────────────────────
        public void HandleConnectionClick(CanvasSlot slot)
        {
            if (slot == null || slot.IsEmpty) return;

            if (_lineStartSlot == null)
            {
                // Biramo početni entitet
                _lineStartSlot = slot;
                slot.IsSelected = true;
            }
            else
            {
                if (_lineStartSlot == slot)
                {
                    // Klik na isti — otkazujemo
                    _lineStartSlot.IsSelected = false;
                    _lineStartSlot = null;
                    return;
                }

                // Sprečavamo duple linije
                bool alreadyConnected = Connections.Any(c =>
                    (c.SlotA == _lineStartSlot && c.SlotB == slot) ||
                    (c.SlotA == slot && c.SlotB == _lineStartSlot));

                if (!alreadyConnected)
                {
                    var connection = new ConnectionLine
                    {
                        SlotA = _lineStartSlot,
                        SlotB = slot
                    };
                    Connections.Add(connection);
                    _undoStack.Push(new AddConnectionAction(Connections, connection));
                }

                _lineStartSlot.IsSelected = false;
                _lineStartSlot = null;
                RefreshConnections();
            }
        }

        // ── Osvežavanje pozicija linija ───────────────────────────────
        public void RefreshConnections()
        {
            const double cellWidth = 100;
            const double cellHeight = 90;
            const int cols = 3;

            foreach (var conn in Connections)
            {
                int indexA = conn.SlotA.Index;
                int indexB = conn.SlotB.Index;

                conn.X1 = (indexA % cols) * cellWidth + cellWidth / 2;
                conn.Y1 = (indexA / cols) * cellHeight + cellHeight / 2;
                conn.X2 = (indexB % cols) * cellWidth + cellWidth / 2;
                conn.Y2 = (indexB / cols) * cellHeight + cellHeight / 2;
            }
        }

        // ── Pomeranje linija pri Drag&Drop ────────────────────────────
        public void UpdateConnectionsForMove(CanvasSlot source, CanvasSlot target)
        {
            foreach (var conn in Connections)
            {
                if (conn.SlotA == source) conn.SlotA = target;
                if (conn.SlotB == source) conn.SlotB = target;
            }
        }

        // ── Uklanjanje entiteta sa canvas-a (pri brisanju iz liste) ──
        public void RemoveEntityFromCanvas(ServerEntity entity)
        {
            var slot = CanvasSlots.FirstOrDefault(s => s.Entity == entity);
            if (slot == null) return;

            // Uklanjamo sve linije vezane za ovaj slot
            var toRemove = Connections.Where(c => c.SlotA == slot || c.SlotB == slot).ToList();
            foreach (var conn in toRemove)
                Connections.Remove(conn);

            slot.Entity = null;
            RefreshTreeView();
        }

        // ── Undo ─────────────────────────────────────────────────────
        private void ExecuteUndo()
        {
            if (_undoStack.Count > 0)
            {
                _undoStack.Pop().Undo();
                RefreshTreeView();
                RefreshConnections();
            }
        }
    }

    // ── Pomoćne klase ─────────────────────────────────────────────────

    public class CanvasSlot : BaseViewModel
    {
        private ServerEntity _entity;
        private bool _isSelected;

        public int Index { get; set; }

        public ServerEntity Entity
        {
            get => _entity;
            set
            {
                _entity = value;
                OnPropertyChanged(nameof(Entity));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsOccupied));
            }
        }

        public bool IsEmpty => _entity == null;
        public bool IsOccupied => _entity != null;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
    }

    public class EntityGroup
    {
        public string TypeName { get; set; }
        public ObservableCollection<ServerEntity> Entities { get; set; }
    }

    public class ConnectionLine : BaseViewModel
    {
        private double _x1, _y1, _x2, _y2;
        public CanvasSlot SlotA { get; set; }
        public CanvasSlot SlotB { get; set; }

        public double X1 { get => _x1; set { _x1 = value; OnPropertyChanged(nameof(X1)); } }
        public double Y1 { get => _y1; set { _y1 = value; OnPropertyChanged(nameof(Y1)); } }
        public double X2 { get => _x2; set { _x2 = value; OnPropertyChanged(nameof(X2)); } }
        public double Y2 { get => _y2; set { _y2 = value; OnPropertyChanged(nameof(Y2)); } }
    }

    // ── Undo akcije za NetworkDisplay ─────────────────────────────────

    public class DropFromTreeAction : IUndoAction
    {
        private readonly CanvasSlot _slot;
        private readonly ServerEntity _entity;
        private readonly NetworkDisplayViewModel _vm;

        public DropFromTreeAction(CanvasSlot slot, ServerEntity entity, NetworkDisplayViewModel vm)
        {
            _slot = slot;
            _entity = entity;
            _vm = vm;
        }

        public void Undo()
        {
            // Uklanjamo linije vezane za ovaj slot
            var toRemove = _vm.Connections
                .Where(c => c.SlotA == _slot || c.SlotB == _slot).ToList();
            foreach (var c in toRemove) _vm.Connections.Remove(c);

            _slot.Entity = null;
        }
    }

    public class MoveOnCanvasAction : IUndoAction
    {
        private readonly CanvasSlot _source;
        private readonly CanvasSlot _target;
        private readonly ServerEntity _entity;
        private readonly NetworkDisplayViewModel _vm;

        public MoveOnCanvasAction(CanvasSlot source, CanvasSlot target,
                                  ServerEntity entity, NetworkDisplayViewModel vm)
        {
            _source = source;
            _target = target;
            _entity = entity;
            _vm = vm;
        }

        public void Undo()
        {
            _vm.UpdateConnectionsForMove(_target, _source);
            _source.Entity = _entity;
            _target.Entity = null;
        }
    }

    public class AddConnectionAction : IUndoAction
    {
        private readonly ObservableCollection<ConnectionLine> _connections;
        private readonly ConnectionLine _connection;

        public AddConnectionAction(ObservableCollection<ConnectionLine> connections,
                                   ConnectionLine connection)
        {
            _connections = connections;
            _connection = connection;
        }

        public void Undo() => _connections.Remove(_connection);
    }
}