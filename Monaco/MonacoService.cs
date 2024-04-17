using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;
using OmniSharp.Models.SignatureHelp;
using OmniSharp.Models.v1.Completion;
using OmniSharp.Options;
using System.Text;


public class MonacoService
{
    #region Fields

    RoslynProject _completionProject;
    RoslynProject _diagnosticProject;
    OmniSharpCompletionService _completionService;
    OmniSharpSignatureHelpService _signatureService;
    OmniSharpQuickInfoProvider _quickInfoProvider;

    JsonSerializerOptions jsonOptions = new JsonSerializerOptions {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };



    #endregion

    #region Records

    public record Diagnostic()
    {
        public LinePosition Start { get; init; }
        public LinePosition End { get; init; }
        public string Message { get; init; }
        public int Severity { get; init; }
    }

    #endregion

    #region Constructors

    public MonacoService()
    {
        DefaultCode =
$@"using System; 
    class Filter 
    {{               
        public Filter() 
        {{ 
            
        }}
    }} 
";
    }

    #endregion

    #region Properties

    public string DefaultCode { get; init; }

    #endregion

    #region Methods

    internal record ResponsePayload(object? Payload, string? Type);

    public async void Init(string uri)
    {
        _completionProject = new RoslynProject(uri);
        await _completionProject.Init();
        _diagnosticProject = new RoslynProject(uri);
        await _diagnosticProject.Init();

        var loggerFactory = LoggerFactory.Create(configure => { });
        var formattingOptions = new FormattingOptions();

        _completionService = new OmniSharpCompletionService(_completionProject.Workspace, formattingOptions, loggerFactory);
        _signatureService = new OmniSharpSignatureHelpService(_completionProject.Workspace);
        _quickInfoProvider = new OmniSharpQuickInfoProvider(_completionProject.Workspace, formattingOptions, loggerFactory);

    }

    public async Task<byte[]> GetCompletionAsync(string code, string completionRequestString)
    {
        Solution updatedSolution;
        var completionRequest = JsonSerializer.Deserialize<CompletionRequest>(completionRequestString);
        do
        {
            updatedSolution = _completionProject.Workspace.CurrentSolution.WithDocumentText(_completionProject.DocumentId, SourceText.From(code));
        } while (!_completionProject.Workspace.TryApplyChanges(updatedSolution));

        var document = updatedSolution.GetDocument(_completionProject.DocumentId);
        var completionResponse = await _completionService.Handle(completionRequest, document);
        

        ResponsePayload p = new ResponsePayload(completionResponse, "GetCompletionAsync");
        
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p, jsonOptions));
    }

    public async Task<byte[]> GetCompletionResolveAsync(string completionResolveRequestString)
    {
        var completionResolveRequest = JsonSerializer.Deserialize<CompletionResolveRequest>(completionResolveRequestString);
        var document = _completionProject.Workspace.CurrentSolution.GetDocument(_completionProject.DocumentId);
        var completionResponse = await _completionService.Handle(completionResolveRequest, document);

        ResponsePayload p = new ResponsePayload(completionResponse, "GetCompletionResolveAsync");

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p, jsonOptions));
    }

    public async Task<byte[]> GetSignatureHelpAsync(string code, string signatureHelpRequestString)
    {
        Solution updatedSolution;
        var signatureHelpRequest = JsonSerializer.Deserialize<SignatureHelpRequest>(signatureHelpRequestString);
        do
        {
            updatedSolution = _completionProject.Workspace.CurrentSolution.WithDocumentText(_completionProject.DocumentId, SourceText.From(code));
        } while (!_completionProject.Workspace.TryApplyChanges(updatedSolution));

        var document = updatedSolution.GetDocument(_completionProject.DocumentId);
        var signatureHelpResponse = await _signatureService.Handle(signatureHelpRequest, document);

        ResponsePayload p = new ResponsePayload(signatureHelpResponse, "GetSignatureHelpAsync");
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p, jsonOptions));
    }

    public async Task<byte[]> GetQuickInfoAsync(string quickInfoRequestString)
    {
        var quickInfoRequest = JsonSerializer.Deserialize<QuickInfoRequest>(quickInfoRequestString);
        
        var document = _diagnosticProject.Workspace.CurrentSolution.GetDocument(_diagnosticProject.DocumentId);
        var quickInfoResponse = await _quickInfoProvider.Handle(quickInfoRequest, document);
        
        ResponsePayload p = new ResponsePayload(quickInfoResponse, "GetQuickInfoAsync");
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p, jsonOptions));
    }

    public async Task<byte[]> GetDiagnosticsAsync(string code)
    {
        Solution updatedSolution;
        do
        {
            updatedSolution = _diagnosticProject.Workspace.CurrentSolution.WithDocumentText(_diagnosticProject.DocumentId, SourceText.From(code));
        } while (!_diagnosticProject.Workspace.TryApplyChanges(updatedSolution));
        var document = updatedSolution.GetDocument(_diagnosticProject.DocumentId);
        var st = await document.GetSyntaxTreeAsync();

        var compilation =
        CSharpCompilation
            .Create("Temp",
                [st],
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: true,
                optimizationLevel: OptimizationLevel.Debug),
                references: RoslynProject.MetadataReferences
            );

        using (var temp = new MemoryStream())
        {
            var result = compilation.Emit(temp);
            var semanticModel = compilation.GetSemanticModel(st, true);

            var dotnetDiagnostics = result.Diagnostics;

            var diagnostics = dotnetDiagnostics.Select(current =>
            {
                var lineSpan = current.Location.GetLineSpan();

                return new Diagnostic()
                {
                    Start = lineSpan.StartLinePosition,
                    End = lineSpan.EndLinePosition,
                    Message = current.GetMessage(),
                    Severity = this.GetSeverity(current.Severity)
                };
            }).ToList();
            ResponsePayload p = new ResponsePayload(diagnostics, "GetDiagnosticsAsync");
            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p, jsonOptions));
        }
    }

    private int GetSeverity(DiagnosticSeverity severity)
    {
        return severity switch
        {
            DiagnosticSeverity.Hidden => 1,
            DiagnosticSeverity.Info => 2,
            DiagnosticSeverity.Warning => 4,
            DiagnosticSeverity.Error => 8,
            _ => throw new Exception("Unknown diagnostic severity.")
        };
    }

    #endregion
}
