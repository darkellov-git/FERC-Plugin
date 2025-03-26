using FERCPlugin.Core.Helpers;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace FERCPlugin.Main.Host;
public abstract class RevitControllerBase {
    protected readonly RevitTaskRunner _taskRunner;

    public RevitControllerBase(RevitTaskRunner taskRunner) =>
        _taskRunner = taskRunner;

    protected async Task<T> GetRequestBody<T>(HttpListenerRequest request) {
        using var reader = new StreamReader(request.InputStream,
                                            request.ContentEncoding);
        var json = await reader.ReadToEndAsync();
        return JsonConvert.DeserializeObject<T>(json);
    }

    protected async Task Ok(HttpListenerResponse response, object data) =>
        await SendResponse(response, data, 200);

    protected async Task BadRequest(HttpListenerResponse response, object data) =>
        await SendResponse(response, data, 400);

    protected async Task StatusCode(HttpListenerResponse response,
                                    int statusCode,
                                    object data) =>
        await SendResponse(response, data, statusCode);

    private async Task SendResponse(HttpListenerResponse response,
                                    object data,
                                    int statusCode) {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";

        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        using var writer = new StreamWriter(response.OutputStream);
        await writer.WriteAsync(json);
    }
}
