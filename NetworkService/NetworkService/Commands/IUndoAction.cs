using NetworkService.Model;
using NetworkService.Model.NetworkService.Model;
using System.Collections.ObjectModel;

namespace NetworkService.Commands
{
    public interface IUndoAction
    {
        void Undo();
    }

    public class AddEntityAction : IUndoAction
    {
        private readonly ObservableCollection<ServerEntity> _entities;
        private readonly ServerEntity _entity;

        public AddEntityAction(ObservableCollection<ServerEntity> entities, ServerEntity entity)
        {
            _entities = entities;
            _entity = entity;
        }

        public void Undo() => _entities.Remove(_entity);
    }

    public class DeleteEntityAction : IUndoAction
    {
        private readonly ObservableCollection<ServerEntity> _entities;
        private readonly ServerEntity _entity;
        private readonly int _index;

        public DeleteEntityAction(ObservableCollection<ServerEntity> entities,
                                  ServerEntity entity, int index)
        {
            _entities = entities;
            _entity = entity;
            _index = index;
        }

        public void Undo()
        {
            if (_index <= _entities.Count)
                _entities.Insert(_index, _entity);
            else
                _entities.Add(_entity);
        }
    }
}