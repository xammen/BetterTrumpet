using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EarTrumpet.DataModel
{
    /// <summary>
    /// Tracks volume and mute changes for undo/redo support.
    /// Keeps a stack of volume actions (device or app volume/mute changes)
    /// with Ctrl+Z (undo) and Ctrl+Y (redo) support.
    /// 
    /// Max stack depth: 50 actions. Oldest actions are discarded.
    /// </summary>
    public class VolumeUndoService
    {
        public class VolumeAction
        {
            public string TargetId { get; set; }
            public string DisplayName { get; set; }
            public bool IsDevice { get; set; }
            public int OldVolume { get; set; }
            public int NewVolume { get; set; }
            public bool? OldMuted { get; set; }
            public bool? NewMuted { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private const int MaxStackDepth = 50;
        private readonly List<VolumeAction> _undoStack = new List<VolumeAction>();
        private readonly List<VolumeAction> _redoStack = new List<VolumeAction>();
        private bool _isUndoRedoing;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Fired when undo/redo state changes (for UI bindings).
        /// </summary>
        public event Action StateChanged;

        /// <summary>
        /// Record a volume change. Call this BEFORE applying the change.
        /// </summary>
        public void RecordVolumeChange(string targetId, string displayName, bool isDevice, int oldVolume, int newVolume)
        {
            if (_isUndoRedoing) return;
            if (oldVolume == newVolume) return;

            // Coalesce rapid changes on the same target (within 500ms)
            if (_undoStack.Count > 0)
            {
                var last = _undoStack[_undoStack.Count - 1];
                if (last.TargetId == targetId &&
                    last.OldMuted == null &&
                    (DateTime.Now - last.Timestamp).TotalMilliseconds < 500)
                {
                    // Update the "new" value of the existing entry instead of creating a new one
                    last.NewVolume = newVolume;
                    last.Timestamp = DateTime.Now;
                    _redoStack.Clear();
                    StateChanged?.Invoke();
                    return;
                }
            }

            Push(new VolumeAction
            {
                TargetId = targetId,
                DisplayName = displayName,
                IsDevice = isDevice,
                OldVolume = oldVolume,
                NewVolume = newVolume,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Record a mute state change. Call this BEFORE applying the change.
        /// </summary>
        public void RecordMuteChange(string targetId, string displayName, bool isDevice, bool oldMuted, bool newMuted)
        {
            if (_isUndoRedoing) return;
            if (oldMuted == newMuted) return;

            Push(new VolumeAction
            {
                TargetId = targetId,
                DisplayName = displayName,
                IsDevice = isDevice,
                OldMuted = oldMuted,
                NewMuted = newMuted,
                Timestamp = DateTime.Now
            });
        }

        private void Push(VolumeAction action)
        {
            _undoStack.Add(action);
            if (_undoStack.Count > MaxStackDepth)
            {
                _undoStack.RemoveAt(0);
            }
            _redoStack.Clear();
            StateChanged?.Invoke();
            Trace.WriteLine($"UndoService: Recorded {(action.IsDevice ? "device" : "app")} " +
                $"'{action.DisplayName}' " +
                $"{(action.OldMuted.HasValue ? $"mute {action.OldMuted}→{action.NewMuted}" : $"vol {action.OldVolume}→{action.NewVolume}")}");
        }

        /// <summary>
        /// Get the action to undo (returns null if nothing to undo).
        /// The caller is responsible for applying the old values.
        /// </summary>
        public VolumeAction Undo()
        {
            if (_undoStack.Count == 0) return null;

            var action = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(action);
            StateChanged?.Invoke();

            Trace.WriteLine($"UndoService: Undo '{action.DisplayName}' " +
                $"{(action.OldMuted.HasValue ? $"mute→{action.OldMuted}" : $"vol→{action.OldVolume}")}");
            return action;
        }

        /// <summary>
        /// Get the action to redo (returns null if nothing to redo).
        /// The caller is responsible for applying the new values.
        /// </summary>
        public VolumeAction Redo()
        {
            if (_redoStack.Count == 0) return null;

            var action = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            _undoStack.Add(action);
            StateChanged?.Invoke();

            Trace.WriteLine($"UndoService: Redo '{action.DisplayName}' " +
                $"{(action.NewMuted.HasValue ? $"mute→{action.NewMuted}" : $"vol→{action.NewVolume}")}");
            return action;
        }

        /// <summary>
        /// Suppress undo recording while applying undo/redo values.
        /// </summary>
        public void BeginUndoRedo() => _isUndoRedoing = true;
        public void EndUndoRedo() => _isUndoRedoing = false;

        /// <summary>
        /// Clear all undo/redo history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
        }

        /// <summary>
        /// Description of what will be undone (for tooltip/status).
        /// </summary>
        public string UndoDescription
        {
            get
            {
                if (_undoStack.Count == 0) return null;
                var a = _undoStack[_undoStack.Count - 1];
                if (a.OldMuted.HasValue)
                    return $"Undo {(a.NewMuted.Value ? "mute" : "unmute")} {a.DisplayName}";
                return $"Undo volume {a.DisplayName} ({a.OldVolume}→{a.NewVolume})";
            }
        }

        /// <summary>
        /// Description of what will be redone (for tooltip/status).
        /// </summary>
        public string RedoDescription
        {
            get
            {
                if (_redoStack.Count == 0) return null;
                var a = _redoStack[_redoStack.Count - 1];
                if (a.NewMuted.HasValue)
                    return $"Redo {(a.NewMuted.Value ? "mute" : "unmute")} {a.DisplayName}";
                return $"Redo volume {a.DisplayName} ({a.OldVolume}→{a.NewVolume})";
            }
        }
    }
}
