namespace Abb2kTools.Commands
{
    public interface IContinuousCommand : ICommand
    {
        /// <summary>
        /// Called every frame while the continuous action is active.
        /// </summary>
        void ExecuteContinuous();

        /// <summary>
        /// Called once when the continuous action ends (e.g., mouse release).
        /// </summary>
        void Complete();
    }
}