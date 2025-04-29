using System.Runtime.InteropServices;

namespace Raphael;

public sealed unsafe class Solver : IDisposable
{
    private nuint cancelFlag = nuint.Zero;
    private readonly Lock solveLock = new();

    private delegate void OnStartDelegate(bool* flag_ptr);
    private delegate void OnFinishDelegate(Action* actions, nuint length);
    private delegate void OnSuggestSolutionDelegate(Action* actions, nuint length);
    private delegate void OnProgressDelegate(nuint progress);
    private delegate void OnLogDelegate(byte* data, nuint length);

    private readonly OnStartDelegate onStart;
    private readonly OnFinishDelegate onFinish;
    private readonly OnSuggestSolutionDelegate onSuggestSolution;
    private readonly OnProgressDelegate onProgress;
    private readonly OnLogDelegate onLog;

    private readonly SolveArgs solveArgs;

    public event Action<Action[]>? OnFinish;
    public event Action<Action[]>? OnSuggestSolution;
    public event Action<nuint>? OnProgress;
    public event Action<string>? OnLog;

    public Solver(in SolverConfig config, in SolverInput input, IEnumerable<Action> pool)
    {
        onStart = (bool* flag_ptr) =>
            Interlocked.Exchange(ref cancelFlag, (nuint)flag_ptr);
        onFinish = (Action* actions, nuint length) =>
        {
            Interlocked.Exchange(ref cancelFlag, nuint.Zero);
            OnFinish?.Invoke(ConvertRawActions(actions, length));
        };
        onSuggestSolution = (Action* actions, nuint length) =>
            OnSuggestSolution?.Invoke(ConvertRawActions(actions, length));
        onProgress = (nuint progress) =>
            OnProgress?.Invoke(progress);
        onLog = (byte* data, nuint length) =>
            OnLog?.Invoke(ConvertRawString(data, length));

        var mask = 0UL;
        foreach (var action in pool)
            mask |= 1UL << (int)action;

        solveArgs = new()
        {
            on_start = (delegate* unmanaged[Cdecl]<bool*, void>)Marshal.GetFunctionPointerForDelegate(onStart),
            on_finish = (delegate* unmanaged[Cdecl]<Action*, nuint, void>)Marshal.GetFunctionPointerForDelegate(onFinish),
            on_suggest_solution = (delegate* unmanaged[Cdecl]<Action*, nuint, void>)Marshal.GetFunctionPointerForDelegate(onSuggestSolution),
            on_progress = (delegate* unmanaged[Cdecl]<nuint, void>)Marshal.GetFunctionPointerForDelegate(onProgress),
            on_log = (delegate* unmanaged[Cdecl]<byte*, nuint, void>)Marshal.GetFunctionPointerForDelegate(onLog),
            log_level = config.LogLevel,

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
        var flag = (bool*)Interlocked.Exchange(ref this.cancelFlag, nuint.Zero);
        if (flag != null)
            *flag = true;
    }

    private static Action[] ConvertRawActions(Action* actions, nuint length) =>
        new Span<Action>(actions, checked((int)length)).ToArray();

    private static string ConvertRawString(byte* data, nuint length) =>
        System.Text.Encoding.UTF8.GetString(data, checked((int)length));

    public void Dispose()
    {
        Cancel();
        lock (solveLock) { }
    }
}
