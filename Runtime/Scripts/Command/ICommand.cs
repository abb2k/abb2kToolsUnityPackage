using System;

namespace Abb2kTools.Commands
{
    public struct CommandMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; private set; }

        public CommandMetadata(string name, string description = "")
        {
            Name = name;
            Description = description;
            Timestamp = DateTime.Now;
        }
    }

    public interface ICommand
    {
        CommandMetadata Metadata { get; }
        void Execute();
        void Undo();
    }
}