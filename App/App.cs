using Microsoft.AspNetCore.Components;
using BlazorWorker.Core;
using Microsoft.JSInterop;

namespace IntelliSage {
    public class App : ComponentBase
{
    [Inject]
    NavigationManager NavigationManager { get; set; }
    [Inject]
    IJSRuntime JS { get; set; }
    [Inject]
    IWorkerFactory workerFactory { get; set; }
    protected override void OnInitialized()
    {
        Intellisage.Init(JS, NavigationManager, workerFactory);
    }
}
}
