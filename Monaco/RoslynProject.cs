using System.Reflection;
using System.ComponentModel;
using System.Data;
using System.Xml;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

public class AssemblyMetadataHelper
{

    private HttpClient _httpClient = new HttpClient();


    public AssemblyMetadataHelper(string uri)
    {
        _httpClient.BaseAddress = new Uri(uri);
    }
    public async Task<MetadataReference?> GetAssemblyMetadataReference(Assembly assembly)
    {
        MetadataReference? ret = null;
        var assemblyName = assembly.GetName().Name ?? "";
        var assemblyUrl = $"./_framework/{assemblyName}.dll";
        try
        {
            var tmp = await _httpClient.GetAsync(assemblyUrl);
            if (tmp.IsSuccessStatusCode)
            {
                var bytes = await tmp.Content.ReadAsByteArrayAsync();
                Console.WriteLine($"Fetching assembly: {assemblyName}");
                if (assemblyName == "System.Runtime")
                {
                    var docProviderFetch = await _httpClient.GetAsync($"./System.Runtime.xml");
                    var docProviderBytes = await docProviderFetch.Content.ReadAsByteArrayAsync();
                    var documentationProvider = XmlDocumentationProvider.CreateFromBytes(docProviderBytes);
                    ret = MetadataReference.CreateFromImage(bytes, documentation: documentationProvider);
                }
                else
                {
                    ret = MetadataReference.CreateFromImage(bytes);
                }
                ret = MetadataReference.CreateFromImage(bytes);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error fetching metadata {e.Message}");
        }
        return ret;
    }
}


public class RoslynProject
{
    private List<Assembly> Assemblies = new List<Assembly>();
    public static List<MetadataReference> MetadataReferences = new List<MetadataReference>();
    private string Uri {get; init;}
    public RoslynProject(string uri)
    {
        Uri = uri;

        // Assemblies we reference for metadata
        Assemblies.Add(Assembly.GetExecutingAssembly());
        Assemblies.Add(Assembly.Load("System.Runtime"));
        Assemblies.Add(Assembly.Load("System.Core"));
        Assemblies.Add(Assembly.Load("System.Collections"));
        Assemblies.Add(Assembly.Load("netstandard"));
        Assemblies.Add(Assembly.Load("System"));
        Assemblies.Add(typeof(Console).Assembly);
        Assemblies.Add(typeof(List<>).Assembly);
        Assemblies.Add(typeof(DescriptionAttribute).Assembly);
        Assemblies.Add(typeof(Task).Assembly);
        Assemblies.Add(typeof(Enumerable).Assembly);
        Assemblies.Add(typeof(DataSet).Assembly);
        Assemblies.Add(typeof(XmlDocument).Assembly);
        Assemblies.Add(typeof(INotifyPropertyChanged).Assembly);
        Assemblies.Add(typeof(System.Linq.Expressions.Expression).Assembly);

    }

    public async Task Init()
    {
        var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
        Workspace = new AdhocWorkspace(host);

        if (MetadataReferences.Count == 0)
        {
            var mh = new AssemblyMetadataHelper(Uri);

            
            foreach (var a in Assemblies)
            {
                try
                {
                    var metadataReference = await mh.GetAssemblyMetadataReference(a);
                    if (metadataReference == null)
                    {
                        Console.WriteLine($"Did not get metadata ref {a.FullName}");
                        continue;
                    }
                    MetadataReferences.Add(metadataReference);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Could not add rdrf {e.Message}");
                }
            }
        }


        var projectInfo = ProjectInfo
            .Create(ProjectId.CreateNewId(), VersionStamp.Create(), "IntelliSage", "IntelliSage", LanguageNames.CSharp)
            .WithMetadataReferences(MetadataReferences)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithParseOptions(CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.LatestMajor));

        var project = Workspace.AddProject(projectInfo);

        UseOnlyOnceDocument = Workspace.AddDocument(project.Id, "Code.cs", SourceText.From(string.Empty));
        DocumentId = UseOnlyOnceDocument.Id;

    }

    public AdhocWorkspace Workspace { get; set; }

    public Document UseOnlyOnceDocument { get; set; }

    public DocumentId DocumentId { get; set; }
}
