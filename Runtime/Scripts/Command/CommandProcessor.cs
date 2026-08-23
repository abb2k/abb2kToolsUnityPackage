using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Abb2kTools.Commands
{
    [Serializable]
    public class CommandProcessor
    {
        // ==========================================
        // EVENTS
        // ==========================================
        
        /// <summary>Fired when a command is executed. Passes the command and the optional reason.</summary>
        public event UnityAction<ICommand, string> OnCommandExecuted;
        
        /// <summary>Fired when a command is undone. Passes the command that was reverted.</summary>
        public event UnityAction<ICommand> OnCommandUndone;
        
        /// <summary>Fired when a command is redone. Passes the command that was reapplied.</summary>
        public event UnityAction<ICommand> OnCommandRedone;
        
        /// <summary>Fired when the history is completely cleared.</summary>
        public event UnityAction OnHistoryCleared;

        // ==========================================
        // SERIALIZED STATE
        // ==========================================

        // [SerializeReference] is REQUIRED for Unity to save Lists of Interfaces!
        [SerializeReference] private List<ICommand> _history = new List<ICommand>();
        [SerializeField] private List<string> _historyReasons = new List<string>();
        [SerializeField] private int _currentIndex = -1;
        [SerializeField] private int _maxHistorySize = 50;
        [SerializeField] private bool _isUnlimited = true;

        [NonSerialized] private int _lastKnownIndex = -2; 
        [NonSerialized] private List<ICommand> _shadowHistory;

        private IContinuousCommand _activeContinuousCommand;

        // ==========================================
        // PROPERTIES
        // ==========================================

        public IReadOnlyList<ICommand> History => _history.AsReadOnly();
        public IReadOnlyList<string> HistoryReasons => _historyReasons.AsReadOnly();
        
        public int CurrentIndex => _currentIndex;
        public bool IsExecutingContinuous => _activeContinuousCommand != null;
        public bool CanUndo => _currentIndex >= 0 && _currentIndex < _history.Count;
        public bool CanRedo => _currentIndex >= -1 && _currentIndex < _history.Count - 1;

        public bool IsUnlimited
        {
            get => _isUnlimited;
            set
            {
                _isUnlimited = value;
                TrimHistoryIfNeeded();
            }
        }

        public int MaxHistorySize
        {
            get => _maxHistorySize;
            set
            {
                _maxHistorySize = Mathf.Max(1, value);
                TrimHistoryIfNeeded();
            }
        }

        public CommandProcessor(bool isUnlimited = true, int maxHistorySize = 50)
        {
            _isUnlimited = isUnlimited;
            _maxHistorySize = maxHistorySize;
        }

        // ==========================================
        // EXECUTION LOGIC
        // ==========================================

        public void ExecuteCommand(ICommand command, string reason = "") 
        {
            if (_activeContinuousCommand != null) EndContinuousCommand();
            
            command.Execute();
            AddCommandToHistory(command, reason);
            
            OnCommandExecuted?.Invoke(command, reason);
        }

        public void BeginContinuousCommand(IContinuousCommand command)
        {
            if (_activeContinuousCommand != null) EndContinuousCommand();
            _activeContinuousCommand = command;
            _activeContinuousCommand.ExecuteContinuous();
        }

        public void UpdateContinuousCommand()
        {
            _activeContinuousCommand?.ExecuteContinuous();
        }

        public void EndContinuousCommand(string reason = "")
        {
            if (_activeContinuousCommand == null) return;
            
            var cmd = _activeContinuousCommand;
            cmd.Complete();
            AddCommandToHistory(cmd, reason);
            _activeContinuousCommand = null;
            
            OnCommandExecuted?.Invoke(cmd, reason);
        }

        public void CancelContinuousCommand()
        {
            if (_activeContinuousCommand == null) return;
            _activeContinuousCommand.Undo();
            _activeContinuousCommand = null;
        }

        private void AddCommandToHistory(ICommand command, string reason = "")
        {
            if (_currentIndex < _history.Count - 1)
            {
                int removeCount = _history.Count - (_currentIndex + 1);
                _history.RemoveRange(_currentIndex + 1, removeCount);
                _historyReasons.RemoveRange(_currentIndex + 1, removeCount);
            }

            _history.Add(command);
            _historyReasons.Add(reason);

            if (!_isUnlimited && _history.Count > _maxHistorySize)
            {
                _history.RemoveAt(0);
                _historyReasons.RemoveAt(0);
            }
            else
            {
                _currentIndex++;
            }
            
            UpdateTracking(); // <-- UPDATED
        }

        private void TrimHistoryIfNeeded()
        {
            if (!_isUnlimited && _history.Count > _maxHistorySize)
            {
                int excess = _history.Count - _maxHistorySize;
                _history.RemoveRange(0, excess);
                _historyReasons.RemoveRange(0, excess);
                _currentIndex -= excess;
                if (_currentIndex < -1) _currentIndex = -1;
            }
        }

        // ==========================================
        // UNDO / REDO / JUMP
        // ==========================================

        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _history[_currentIndex];
            cmd.Undo();
            _currentIndex--;
            UpdateTracking(); // <-- UPDATED
            OnCommandUndone?.Invoke(cmd);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            _currentIndex++;
            var cmd = _history[_currentIndex];
            cmd.Execute();
            UpdateTracking(); // <-- UPDATED
            OnCommandRedone?.Invoke(cmd);
        }

        public void ClearHistory()
        {
            _history.Clear();
            _historyReasons.Clear();
            _currentIndex = -1;
            _activeContinuousCommand = null;
            UpdateTracking(); // <-- UPDATED
            OnHistoryCleared?.Invoke();
        }
        
        public void JumpTo(int targetIndex)
        {
            if (_currentIndex >= _history.Count) _currentIndex = _history.Count - 1;
            int clampedTarget = Math.Max(-1, Math.Min(targetIndex, _history.Count - 1));
            
            while (_currentIndex > clampedTarget && CanUndo) Undo();
            while (_currentIndex < clampedTarget && CanRedo) Redo();
        }

        public void SyncUndoState()
        {
            // Initialize on first frame or after a script reload
            if (_shadowHistory == null || _lastKnownIndex == -2) 
            {
                UpdateTracking();
                return;
            }

            if (_lastKnownIndex != _currentIndex)
            {
                int previousIndex = _lastKnownIndex;
                _lastKnownIndex = _currentIndex; // Set immediately to prevent infinite loops

                if (previousIndex > _currentIndex)
                {
                    // Unity Undid our state (Ctrl+Z)
                    for (int i = previousIndex; i > _currentIndex; i--)
                    {
                        // Use SHADOW history because Unity already deleted it from standard history!
                        if (i >= 0 && i < _shadowHistory.Count) 
                        {
                            _shadowHistory[i].Undo();
                        }
                    }
                }
                else if (previousIndex < _currentIndex)
                {
                    // Unity Redid our state (Ctrl+Y)
                    for (int i = previousIndex + 1; i <= _currentIndex; i++)
                    {
                        // Use standard history because Unity just restored it
                        if (i >= 0 && i < _history.Count) 
                        {
                            _history[i].Execute();
                        }
                    }
                }
                
                // Catch the shadow list up to the new reality
                _shadowHistory = new List<ICommand>(_history);
            }
            else if (_history.Count != _shadowHistory.Count)
            {
                // Catch up if capacity changed the list size without changing the index
                _shadowHistory = new List<ICommand>(_history);
            }
        }

        private void UpdateTracking()
        {
            _lastKnownIndex = _currentIndex;
            _shadowHistory = new List<ICommand>(_history);
        }
    }
}