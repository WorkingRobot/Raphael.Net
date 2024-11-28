using Raphael.Net;
using System.Runtime.InteropServices;

namespace Raphael;

public sealed unsafe class Solver : IDisposable
{
    private bool* flag = null;
    private readonly object flagLock = new();
    private readonly object solveLock = new();

    private delegate void OnStartDelegate(bool* flag_ptr);
    private delegate void OnFinishDelegate(Action* actions, nuint length);
    private delegate void OnSuggestSolutionDelegate(Action* actions, nuint length);
    private delegate void OnProgressDelegate(nuint progress);

    private readonly OnStartDelegate onStart;
    private readonly OnFinishDelegate onFinish;
    private readonly OnSuggestSolutionDelegate onSuggestSolution;
    private readonly OnProgressDelegate onProgress;

    private readonly SolveArgs solveArgs;

    public event Action<Action[]>? OnFinish;
    public event Action<Action[]>? OnSuggestSolution;
    public event Action<nuint>? OnProgress;

    public Solver(in SolverConfig config, SolverInput input, IEnumerable<Action> pool)
    {
        onStart = (bool* flag_ptr) =>
        {
            lock (flagLock)
                flag = flag_ptr;
        };
        onFinish = (Action* actions, nuint length) =>
        {
            lock (flagLock)
                flag = null;
            OnFinish?.Invoke(ConvertRawActions(actions, length));
        };
        onSuggestSolution = (Action* actions, nuint length) =>
            OnSuggestSolution?.Invoke(ConvertRawActions(actions, length));
        onProgress = (nuint progress) =>
            OnProgress?.Invoke(progress);

        var mask = 0UL;
        foreach (var action in pool)
            mask |= 1UL << (int)action;

        solveArgs = new()
        {
            on_start = (delegate* unmanaged[Cdecl]<bool*, void>)Marshal.GetFunctionPointerForDelegate(onStart),
            on_finish = (delegate* unmanaged[Cdecl]<Action*, nuint, void>)Marshal.GetFunctionPointerForDelegate(onFinish),
            on_suggest_solution = (delegate* unmanaged[Cdecl]<Action*, nuint, void>)Marshal.GetFunctionPointerForDelegate(onSuggestSolution),
            on_progress = (delegate* unmanaged[Cdecl]<nuint, void>)Marshal.GetFunctionPointerForDelegate(onProgress),

            action_mask = mask,

            durability = input.Durability,
            progress = input.Progress,
            quality = input.Quality,

            cp = input.CP,
            job_level = input.JobLevel,
            base_progress = input.BaseProgressGain,
            base_quality = input.BaseQualityGain,

            adversarial = config.Adversarial,
            backload_progress = config.BackloadProgress,
            unsound_branch_pruning = config.UnsoundBranchPruning,
        };
    }

    public void Solve()
    {
        lock (solveLock)
        {
            var args = solveArgs;
            NativeMethods.solve(&args);
        }
    }

    public void Cancel()
    {
        lock (flagLock)
        {
            if (flag != null)
                *flag = true;
        }
    }

    private static Action[] ConvertRawActions(Action* actions, nuint length) =>
        new Span<Action>(actions, checked((int)length)).ToArray();

    public void Dispose()
    {
        Cancel();
        lock (solveLock) { }
    }
}
