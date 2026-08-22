using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abb2kTools.Commands
{
    [Serializable]
    public class MacroCommand : ICommand
    {

        [SerializeField] private string _macroName = "New Macro";
        private string _macroDescription = "Executes multiple commands";
        [SerializeReference] 
        private List<ICommand> _commands = new List<ICommand>();

        public CommandMetadata Metadata => new CommandMetadata(_macroName, _macroDescription);

        public MacroCommand() { } 

        public MacroCommand(string name, string description = "")
        {
            _macroName = name;
            _macroDescription = description;
        }


        public MacroCommand(string name, string description = "", List<ICommand> commands = default)
        {
            _macroName = name;
            _macroDescription = description;
            _commands = commands;
        }

        public void AddCommand(ICommand command)
        {
            _commands.Add(command);
        }

        public void Execute()
        {
            for (int i = 0; i < _commands.Count; i++) _commands[i].Execute();
        }

        public void Undo()
        {
            for (int i = _commands.Count - 1; i >= 0; i--) _commands[i].Undo();
        }
    }
}