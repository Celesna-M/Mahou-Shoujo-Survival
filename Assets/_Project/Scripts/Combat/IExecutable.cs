public interface IExecutable
{
    bool CanBeExecuted { get; }
    void ExecuteKill();
}