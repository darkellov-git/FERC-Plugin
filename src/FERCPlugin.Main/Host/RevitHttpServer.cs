using FERCPlugin.Core.Helpers;
using System.IO;
using System.Net;

namespace FERCPlugin.Main.Host;

public class RevitHttpServer {
    private HttpListener _listener;
    private bool _isRunning;
    private readonly Dictionary<string, Func<HttpListenerContext, Task>> _routes;

    public RevitHttpServer(RevitTaskRunner taskRunner) {
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:5353/");

        var familyController = new RevitController(taskRunner);
        _routes = new Dictionary<string, Func<HttpListenerContext, Task>> {
                { "/create-family", familyController.HandleCreateFamily },
                { "/create-views", familyController.HandleCreateViews }
            };
    }

    public void Start() {
        if (_isRunning)
            return;

        _listener.Start();
        _isRunning = true;

        Task.Run(async () => {
            while (_listener.IsListening) {
                var context = await _listener.GetContextAsync();
                HandleRequest(context);
            }
        });
    }

    public void Stop() {
        _isRunning = false;
        _listener?.Stop();
    }

    private async void HandleRequest(HttpListenerContext context) {
        try {
            if (_routes.TryGetValue(context.Request.Url.AbsolutePath,
                                    out var handler)) {
                await handler(context);
            } else {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        } catch (Exception ex) {
            context.Response.StatusCode = 500;
            await new StreamWriter(context.Response.OutputStream)
                .WriteAsync($"Error: {ex.Message}");
            context.Response.Close();
        }
    }
}
