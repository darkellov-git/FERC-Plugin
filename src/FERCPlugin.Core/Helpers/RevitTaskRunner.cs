using Autodesk.Revit.UI;

namespace FERCPlugin.Core.Helpers;

/// <summary>
/// This class responsible for the running of
/// </summary>
public class RevitTaskRunner
{
    private readonly EventHandler  _handler;
    private readonly ExternalEvent _externalEvent;

    private TaskCompletionSource<object> _tcs;

    public RevitTaskRunner()
    {
        _handler                =  new EventHandler();
        _handler.EventCompleted += OnEventCompleted;

        _externalEvent = ExternalEvent.Create(_handler);
    }

    public Task<TResult> Run<TResult>(Func<UIApplication, TResult> func)
    {
        _tcs = new TaskCompletionSource<object>();

        Task<TResult> task = Task.Run(async () => (TResult)await _tcs.Task);

        _handler.Func = app => func(app);

        _externalEvent.Raise();

        return task;
    }

    public Task Run(Action<UIApplication> act)
    {
        _tcs = new TaskCompletionSource<object>();

        _handler.Func = (app) =>
        {
            act(app);
            return new object();
        };

        _externalEvent.Raise();

        return _tcs.Task;
    }

    private void OnEventCompleted(object sender, object result)
    {
        if (_handler.Exception == null)
        {
            _tcs.TrySetResult(result);
        }
        else
        {
            _tcs.TrySetException(_handler.Exception);
        }
    }

    private class EventHandler : IExternalEventHandler
    {
        private Func<UIApplication, object> _func;

        public event EventHandler<object> EventCompleted;

        public Exception Exception { get; private set; }

        public Func<UIApplication, object> Func
        {
            get => _func;
            set
                => _func = value ??
                           throw new ArgumentNullException();
        }

        public void Execute(UIApplication app)
        {
            object result = null;
            Exception = null;

            try
            {
                result = Func(app);
            }
            catch (Exception ex)
            {
                Exception = ex;
            }

            EventCompleted?.Invoke(this, result);
        }

        public string GetName()
        {
            return GetType().Name;
        }
    }
}
