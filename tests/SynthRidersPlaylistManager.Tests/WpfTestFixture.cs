using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;

namespace SynthRidersPlaylistManager.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfTestCollection : ICollectionFixture<WpfTestFixture>
{
    public const string Name = "WPF application";
}

public sealed class WpfTestFixture : IDisposable
{
    private readonly BlockingCollection<WorkItem> _work = [];
    private readonly Thread _thread;

    public WpfTestFixture()
    {
        using var ready = new ManualResetEventSlim();
        Exception? startupFailure = null;
        _thread = new Thread(() =>
        {
            try
            {
                var application = new SynthRidersPlaylistManager.App.App();
                application.InitializeComponent();
                application.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                ready.Set();
                foreach (var item in _work.GetConsumingEnumerable()) item.Execute();
                application.Shutdown();
            }
            catch (Exception ex)
            {
                startupFailure = ex;
                ready.Set();
            }
        }) { IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait();
        if (startupFailure is not null) ExceptionDispatchInfo.Capture(startupFailure).Throw();
    }

    public void Run(Action action)
    {
        var item = new WorkItem(action);
        _work.Add(item);
        item.Wait();
    }

    public void Dispose()
    {
        _work.CompleteAdding();
        _thread.Join();
        _work.Dispose();
    }

    private sealed class WorkItem(Action action)
    {
        private readonly ManualResetEventSlim _completed = new();
        private Exception? _failure;

        public void Execute()
        {
            try { action(); }
            catch (Exception ex) { _failure = ex; }
            finally { _completed.Set(); }
        }

        public void Wait()
        {
            _completed.Wait();
            _completed.Dispose();
            if (_failure is not null) ExceptionDispatchInfo.Capture(_failure).Throw();
        }
    }
}
