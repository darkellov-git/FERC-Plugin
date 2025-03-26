using FERCPlugin.Core.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace FERCPlugin.Main.Host;
public class RevitController : RevitControllerBase {
    public RevitController(RevitTaskRunner taskRunner) : base(taskRunner) { }

    [HttpPost("/create-family")]
    public async Task HandleCreateFamily(HttpListenerContext context) {
        try {
            var dto = await GetRequestBody<RequestDto>(context.Request);

            var fileBytes = await _taskRunner.Run(app => {
                var command = new PluginCommands();
                return command.GetFamily(app, dto);
            });

            if (fileBytes == null || fileBytes.Length == 0) {
                await BadRequest(context.Response,
                                new { error = "Revit family creation error" });
                return;
            }

            context.Response.ContentType = "application/octet-stream";
            context.Response.AddHeader("Content-Disposition",
                                       $"attachment; filename={dto.Id}.rfa");

            context.Response.ContentLength64 = fileBytes.Length;

            await context.Response.OutputStream
                .WriteAsync(fileBytes, 0, fileBytes.Length);
            context.Response.OutputStream.Close();
        } catch (Exception ex) {
            await BadRequest(context.Response, new { error = ex.Message });
        }
    }

    [HttpPost("/create-views")]
    public async Task HandleCreateViews(HttpListenerContext context) {
        try {
            var dto = await GetRequestBody<RequestDto>(context.Request);

            var fileBytes = await _taskRunner.Run(app => {
                var command = new PluginCommands();
                return command.GetViews(app, dto);
            });

            if (fileBytes == null || fileBytes.Length == 0) {
                await BadRequest(context.Response,
                                new { error = "Revit views creation error" });
                return;
            }

            // TODO change file extension
            context.Response.ContentType = "application/octet-stream";
            context.Response.AddHeader("Content-Disposition",
                                       $"attachment; filename={dto.Id}.rfa");

            context.Response.ContentLength64 = fileBytes.Length;

            await context.Response.OutputStream
                .WriteAsync(fileBytes, 0, fileBytes.Length);
            context.Response.OutputStream.Close();
        } catch (Exception ex) {
            await BadRequest(context.Response, new { error = ex.Message });
        }
    }
}
