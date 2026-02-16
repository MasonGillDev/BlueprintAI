using System.Text.Json;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Services;

public class BlueprintStateManager
{
    private Blueprint _current = new();
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    public Blueprint Current => _current;

    public void SaveSnapshot()
    {
        _undoStack.Push(JsonSerializer.Serialize(_current));
        _redoStack.Clear();
    }

    public Blueprint? Undo()
    {
        if (_undoStack.Count == 0) return null;
        _redoStack.Push(JsonSerializer.Serialize(_current));
        _current = JsonSerializer.Deserialize<Blueprint>(_undoStack.Pop())!;
        _current.Version++;
        return _current;
    }

    public Blueprint? Redo()
    {
        if (_redoStack.Count == 0) return null;
        _undoStack.Push(JsonSerializer.Serialize(_current));
        _current = JsonSerializer.Deserialize<Blueprint>(_redoStack.Pop())!;
        _current.Version++;
        return _current;
    }

    public BlueprintDelta AddNode(BlueprintNode node)
    {
        SaveSnapshot();
        _current.Nodes.Add(node);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.NodeAdded,
            Node = node,
            Version = _current.Version
        };
    }

    public BlueprintDelta RemoveNode(string nodeId)
    {
        SaveSnapshot();
        var node = _current.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null) throw new InvalidOperationException($"Node {nodeId} not found");

        _current.Nodes.Remove(node);
        _current.Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.NodeRemoved,
            RemovedId = nodeId,
            Version = _current.Version
        };
    }

    public BlueprintDelta UpdateNode(BlueprintNode updated)
    {
        SaveSnapshot();
        var index = _current.Nodes.FindIndex(n => n.Id == updated.Id);
        if (index < 0) throw new InvalidOperationException($"Node {updated.Id} not found");

        _current.Nodes[index] = updated;
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.NodeUpdated,
            Node = updated,
            Version = _current.Version
        };
    }

    public BlueprintDelta AddConnection(Connection connection)
    {
        SaveSnapshot();
        _current.Connections.Add(connection);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.ConnectionAdded,
            Connection = connection,
            Version = _current.Version
        };
    }

    public BlueprintDelta RemoveConnection(string connectionId)
    {
        SaveSnapshot();
        var conn = _current.Connections.FirstOrDefault(c => c.Id == connectionId);
        if (conn == null) throw new InvalidOperationException($"Connection {connectionId} not found");

        _current.Connections.Remove(conn);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.ConnectionRemoved,
            RemovedId = connectionId,
            Version = _current.Version
        };
    }

    public BlueprintDelta AddComment(BlueprintComment comment)
    {
        SaveSnapshot();
        _current.Comments.Add(comment);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.CommentAdded,
            Comment = comment,
            Version = _current.Version
        };
    }

    public BlueprintDelta RemoveComment(string commentId)
    {
        SaveSnapshot();
        var comment = _current.Comments.FirstOrDefault(c => c.Id == commentId);
        if (comment == null) throw new InvalidOperationException($"Comment {commentId} not found");

        _current.Comments.Remove(comment);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.CommentRemoved,
            RemovedId = commentId,
            Version = _current.Version
        };
    }

    public BlueprintDelta AddVariable(BlueprintVariable variable)
    {
        SaveSnapshot();
        _current.Variables.Add(variable);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.VariableAdded,
            Variable = variable,
            Version = _current.Version
        };
    }

    public BlueprintDelta RemoveVariable(string variableId)
    {
        SaveSnapshot();
        var variable = _current.Variables.FirstOrDefault(v => v.Id == variableId);
        if (variable == null) throw new InvalidOperationException($"Variable {variableId} not found");

        _current.Variables.Remove(variable);
        _current.Version++;
        return new BlueprintDelta
        {
            Type = DeltaType.VariableRemoved,
            RemovedId = variableId,
            Version = _current.Version
        };
    }

    public BlueprintDelta GetFullSync()
    {
        return new BlueprintDelta
        {
            Type = DeltaType.FullSync,
            FullState = _current,
            Version = _current.Version
        };
    }
}
