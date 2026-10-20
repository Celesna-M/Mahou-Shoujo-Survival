using UnityEngine;

public interface IExecutable
{
    bool CanBeExecuted { get; }
    Transform Transform { get; }
    void OnExecutionStart();
    void ExecuteKill();
}