using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UIFixes;

// Generic MonoBehaviours can't be injected into il2cpp, so the serializers are plain managed objects
// driven by a single non-generic runner component
public class TaskSerializerRunner : MonoBehaviour
{
    public TaskSerializerRunner(IntPtr pointer) : base(pointer) { }

    public TaskSerializerBase Serializer;

    public void LateUpdate()
    {
        Serializer?.LateUpdate();
    }

    public void OnDisable()
    {
        Serializer?.OnDisable();
    }
}

public abstract class TaskSerializerBase
{
    protected static int GlobalDepth = 0;

    private TaskSerializerRunner _runner;

    public static T Create<T>(GameObject gameObject) where T : TaskSerializerBase, new()
    {
        T serializer = new();
        serializer._runner = gameObject.AddComponent<TaskSerializerRunner>();
        serializer._runner.Serializer = serializer;
        return serializer;
    }

    public abstract void LateUpdate();

    public abstract void OnDisable();

    protected void DestroyRunner()
    {
        if (_runner != null)
        {
            _runner.Serializer = null;
            UnityEngine.Object.Destroy(_runner);
            _runner = null;
        }
    }
}

public class TaskSerializer<T, TaskT> : TaskSerializerBase
{
    private int _depth;
    private Func<T, Task<TaskT>> _func;
    private Func<T, TaskT, bool> _canContinue;
    private IEnumerator<T> _enumerator;
    private Task<TaskT> _currentTask;
    private System.Threading.Tasks.TaskCompletionSource _totalTask;

    public Task Initialize(IEnumerable<T> items, Func<T, Task<TaskT>> func, Func<T, TaskT, bool> canContinue = null)
    {
        _enumerator = items.GetEnumerator();
        _func = func;
        _canContinue = canContinue;

        _currentTask = null;
        _totalTask = new System.Threading.Tasks.TaskCompletionSource();

        ++GlobalDepth;
        _depth = GlobalDepth;

        LateUpdate();

        return _totalTask.Task;
    }

    public void Cancel()
    {
        if (!_totalTask.Task.IsCompleted)
        {
            _totalTask.TrySetCanceled();
            Complete();
        }
    }

    public override void OnDisable()
    {
        Cancel();
    }

    public override void LateUpdate()
    {
        if (_totalTask.Task.IsCompleted)
        {
            return;
        }

        // There is a child task serializer running
        if (GlobalDepth > _depth)
        {
            return;
        }

        if (_currentTask != null)
        {
            if (_currentTask.IsCanceled)
            {
                Complete();
                return;
            }

            if (!_currentTask.IsCompleted)
            {
                return;
            }

            if (_canContinue != null && !_canContinue(_enumerator.Current, _currentTask.Result))
            {
                return;
            }
        }

        if (_enumerator.MoveNext())
        {
            _currentTask = _func(_enumerator.Current);
        }
        else
        {
            Complete();
        }
    }

    private void Complete()
    {
        _totalTask.TrySetResult();
        --GlobalDepth;
        _func = null;
        DestroyRunner();
    }
}

public class TaskSerializer<T> : TaskSerializer<T, bool>
{
    public Task Initialize(IEnumerable<T> items, Action<T> action, Func<T, bool> canContinue = null)
    {
        Task<bool> Func(T t)
        {
            action(t);
            return Task.FromResult(true);
        }

        return base.Initialize(items, Func, canContinue != null ? (x, _) => canContinue(x) : null);
    }

    public Task Initialize(IEnumerable<T> items, Func<T, Task> func, Func<T, bool> canContinue = null)
    {
        async Task<bool> Wrapper(T t)
        {
            try
            {
                await func(t);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        return base.Initialize(items, Wrapper, canContinue != null ? (x, _) => canContinue(x) : null);
    }

}