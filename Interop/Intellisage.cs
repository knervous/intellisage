
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorWorker.Core;
using BlazorWorker.BackgroundServiceFactory;
using BlazorWorker.WorkerBackgroundService;
using System.Text;

public class MonacoServiceWrapper {
    [JSInvokable]
    public async Task<byte[]?> RunAsync(string name, string[] args) {
        switch (name) {
            case "GetCompletionAsync":
                return await Intellisage.MonacoWorkerWrapper.RunAsync(a => a.GetCompletionAsync(args[0], args[1]));
            case "GetCompletionResolveAsync":
                return await Intellisage.MonacoWorkerWrapper.RunAsync(a => a.GetCompletionResolveAsync(args[0]));
            case "GetSignatureHelpAsync":
                return await Intellisage.MonacoWorkerWrapper.RunAsync(a => a.GetSignatureHelpAsync(args[0], args[1]));
            case "GetQuickInfoAsync":
                return await Intellisage.MonacoWorkerWrapper.RunAsync(a => a.GetQuickInfoAsync(args[0]));
            case "GetDiagnosticsAsync":
                return await Intellisage.MonacoWorkerWrapper.RunAsync(a => a.GetDiagnosticsAsync(args[0]));
        }
       return Encoding.UTF8.GetBytes("{}");
    }
}

public static class Intellisage
{
    public static NavigationManager? NavigationManager {get;set;}
    public static IWorkerBackgroundService<MonacoService>? MonacoWorkerWrapper {get;set;}
    public static IWorker? Worker {get; set;}

    public static async void Init(IJSRuntime JS, NavigationManager nm, IWorkerFactory wf)
    {
       NavigationManager = nm;
       Worker = await wf.CreateAsync();
       Console.WriteLine("Creating worker");
       MonacoWorkerWrapper = await Worker.CreateBackgroundServiceAsync<MonacoService>();
       await MonacoWorkerWrapper.RunAsync(a => a.Init(nm.BaseUri));
       var _objRef = DotNetObjectReference.Create(new MonacoServiceWrapper());
       await JS.InvokeAsync<string>("registerService", _objRef);
       Console.WriteLine("Registered service");
    }
    
}