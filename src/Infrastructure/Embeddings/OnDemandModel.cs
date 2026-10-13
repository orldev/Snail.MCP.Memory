using Microsoft.Extensions.Logging;

namespace Snail.MCP.Memory.Infrastructure.Embeddings;

/// <summary>A model loaded on its first use, released once it has gone unused for the idle period, and loaded again on the next.</summary>
/// <remarks>
/// <para>
/// The shared HTTP server lives for weeks, and the embedding model was most of what it held: 202 MB of its 441 MB of
/// footprint, kept through every night and weekend nobody called it. Released, it took the server from 321 MB to
/// 173 MB, and to 137 MB once the idle trim had run; the next recall loaded it again in 0.4 s (2026-10-13). The native
/// blocks the session frees are reclaimed by the kernel some seconds after the release, not at the moment of it.
/// </para>
/// <para>
/// Concurrent first uses share one load. A use counts itself in under the lock before it receives the model, and the
/// unload takes the model under the same lock only when no use is counted, so a model is never released under a call.
/// </para>
/// <para>
/// The release is followed by one compacting collection. Snail.Sdk's idle trim does not make it redundant: it compacts
/// only after the process has allocated 32 MB since its last trim, and a server whose quiet period began long before
/// the unload allocates nothing in between, so the managed side of the model would stay committed until the next burst.
/// </para>
/// </remarks>
/// <param name="load">Loads the model; called on a thread-pool thread, once for every load.</param>
/// <param name="idlePeriod">How long the model may go unused before it is released; zero or less keeps it for good.</param>
/// <param name="time">The clock the idle period is measured by and its timer runs on.</param>
/// <param name="logger">Where the loads and the releases are told.</param>
internal sealed class OnDemandModel<TModel>(Func<Task<TModel>> load, TimeSpan idlePeriod, TimeProvider time, ILogger logger) : IDisposable
    where TModel : class, IDisposable
{
    private readonly Lock _lock = new();

    private Task<TModel>? _loading;
    private TModel? _model;
    private ITimer? _unloadTimer;
    private int _uses;
    private DateTimeOffset? _lastUsed;
    private bool _isDisposed;

    /// <summary>Whether the model is in memory now.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_lock)
                return _model is not null;
        }
    }

    /// <summary>When the last use ended; <c>null</c> before the first one.</summary>
    public DateTimeOffset? LastUsed
    {
        get
        {
            lock (_lock)
                return _lastUsed;
        }
    }

    /// <summary>Runs <paramref name="work"/> with the model, loading it first when it is not in memory.</summary>
    /// <exception cref="ObjectDisposedException">The holder is disposed.</exception>
    public async Task<TResult> UseAsync<TResult>(Func<TModel, Task<TResult>> work, CancellationToken cancellationToken)
    {
        var loading = Enter();

        try
        {
            var model = await loading.WaitAsync(cancellationToken);

            return await work(model);
        }
        finally
        {
            Leave();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        TModel? model;

        lock (_lock)
        {
            _isDisposed = true;
            model = _model;
            _model = null;
            _loading = null;
        }

        _unloadTimer?.Dispose();
        model?.Dispose();
    }

    private Task<TModel> Enter()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _uses++;

            return _loading ??= Task.Run(LoadAsync);
        }
    }

    private void Leave()
    {
        lock (_lock)
        {
            _uses--;
            _lastUsed = time.GetUtcNow();
            ScheduleUnload(idlePeriod);
        }
    }

    private async Task<TModel> LoadAsync()
    {
        var started = time.GetTimestamp();

        TModel model;

        try
        {
            model = await load();
        }
        catch
        {
            lock (_lock)
                _loading = null;

            throw;
        }

        Log.ModelLoaded(logger, time.GetElapsedTime(started).TotalMilliseconds);

        lock (_lock)
        {
            if (!_isDisposed)
            {
                _model = model;
                ScheduleUnload(idlePeriod);

                return model;
            }
        }

        model.Dispose();

        throw new ObjectDisposedException(GetType().FullName);
    }

    private void ScheduleUnload(TimeSpan dueTime)
    {
        if (idlePeriod <= TimeSpan.Zero || _isDisposed)
            return;

        _unloadTimer ??= time.CreateTimer(_ => UnloadIfIdle(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _unloadTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
    }

    private void UnloadIfIdle()
    {
        TModel? model;
        TimeSpan idleFor;

        lock (_lock)
        {
            if (_isDisposed || _model is null || _uses > 0)
                return;

            idleFor = _lastUsed is { } lastUsed ? time.GetUtcNow() - lastUsed : idlePeriod;

            if (idleFor < idlePeriod)
            {
                ScheduleUnload(idlePeriod - idleFor);

                return;
            }

            model = _model;
            _model = null;
            _loading = null;
        }

        model.Dispose();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        Log.ModelUnloaded(logger, idleFor);
    }
}
