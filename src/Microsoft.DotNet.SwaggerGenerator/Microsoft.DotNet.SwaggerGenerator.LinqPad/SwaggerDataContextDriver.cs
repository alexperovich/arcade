using LINQPad.Extensibility.DataContext;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.SwaggerGenerator.Languages;
using Microsoft.DotNet.SwaggerGenerator.Modeler;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class SwaggerDataContextDriver : DynamicDataContextDriver
    {
        public SwaggerDataContextDriver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                var shortName = new AssemblyName(e.Name).Name;
                var dllPath = Path.Combine(GetDriverFolder(), shortName + ".dll");

                if (File.Exists(dllPath))
                {
                    return LoadAssemblySafely(dllPath);
                }

                return null;
            };
        }

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
        {
            return new SwaggerProperties(cxInfo).Uri;
        }

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions)
        {
            return new ConnectionDialog(cxInfo).ShowDialog() == true;
        }

        public override string Name => "Swagger";
        public override string Author => "Microsoft";

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo,
            AssemblyName assemblyToBuild,
            ref string nameSpace,
            ref string typeName)
        {
            var properties = new SwaggerProperties(cxInfo);
            var uri = properties.Uri;

            Templates.BasePath = Path.Combine(Path.GetDirectoryName(typeof(Templates).Assembly.Location), "../../content/");

            var options = new GeneratorOptions
            {
                Namespace = nameSpace,
                ClientName = typeName + "ApiClient",
                LanguageName = "csharp",
            };
            ServiceClientModel model = GetModelAsync(uri, options).GetAwaiter().GetResult();

            var codeFactory = new ServiceClientCodeFactory();
            var code = codeFactory.GenerateCode(model, options, NullLogger.Instance);

            var infoVersion = typeof(SwaggerDataContextDriver).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var contextClass = $@"
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.SwaggerGenerator.LinqPad;
using Azure;
using Azure.Core;

[assembly: System.Reflection.AssemblyInformationalVersion(""{infoVersion.InformationalVersion}"")]

namespace {nameSpace}
{{
    public class {typeName} : {typeName}ApiClient, ISwaggerContext
    {{
        private ISwaggerContextLogger _logger;
        ISwaggerContextLogger ISwaggerContext.SwaggerContextLogger
        {{
            set => _logger = value;
        }}

        public override async ValueTask<Response> SendAsync(Request request, CancellationToken cancellationToken)
        {{
            if (_logger != null)
            {{
                await _logger.RequestStarting(request);
            }}
            var response = await base.SendAsync(request, cancellationToken);
            if (_logger != null)
            {{
                await _logger.RequestFinished(request, response);
            }}

            return response;
        }}
    }}
}}
";
            code.Add(new CodeFile("Context.cs", contextClass));

            BuildAssembly(code, assemblyToBuild);

            return GetSchema(model).ToList();
        }

        public override void InitializeContext(IConnectionInfo cxInfo, object context, QueryExecutionManager executionManager)
        {
            var ctx = (ISwaggerContext) context;
            ctx.SwaggerContextLogger = new SwaggerContextLogger(executionManager.SqlTranslationWriter);
            base.InitializeContext(cxInfo, context, executionManager);
        }

        public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
        {
            return new[]
            {
                "Microsoft.DotNet.SwaggerGenerator.LinqPad.dll",
                "Azure.Core.dll",
                "Microsoft.Bcl.AsyncInterfaces.dll",
                "Newtonsoft.Json.dll",
                "System.Collections.Immutable.dll",
                "System.Net.Http.dll",
            };
        }

        public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
        {
            return new[]
            {
                "Azure.Core",
                "Newtonsoft.Json",
                "Newtonsoft.Json.Linq",
                "System.Collections.Immutable",
            };
        }

        private IEnumerable<ExplorerItem> GetSchema(ServiceClientModel model)
        {
            yield return new ExplorerItem("Definitions", ExplorerItemKind.Category, ExplorerIcon.Box)
            {
                Children = GetDefinitions(model).ToList(),
            };
            yield return new ExplorerItem("Apis", ExplorerItemKind.Category, ExplorerIcon.Box)
            {
                Children = GetApis(model).ToList(),
            };
        }

        private IEnumerable<ExplorerItem> GetDefinitions(ServiceClientModel model)
        {
            foreach (var type in model.Types)
            {
                if (type is EnumTypeModel enumType)
                {
                    yield return EnumExplorerItem(enumType);
                }
                else
                {
                    yield return TypeExplorerItem((ClassTypeModel)type);
                }
            }
        }

        private ExplorerItem EnumExplorerItem(EnumTypeModel type)
        {
            return new ExplorerItem(type.Name, ExplorerItemKind.Schema, ExplorerIcon.Table)
            {
                Children = type.Values.Select(v => new ExplorerItem(v, ExplorerItemKind.Property, ExplorerIcon.Blank)).ToList(),
            };
        }

        private ExplorerItem TypeExplorerItem(ClassTypeModel type)
        {
            return new ExplorerItem(type.Name, ExplorerItemKind.Schema, ExplorerIcon.Table)
            {
                Children = type.Properties.Select(p => new ExplorerItem($"{p.Name}: {p.Type}", ExplorerItemKind.Property, ExplorerIcon.Blank)).ToList(),
            };
        }

        private IEnumerable<ExplorerItem> GetApis(ServiceClientModel model)
        {
            foreach (var group in model.MethodGroups)
            {
                yield return new ExplorerItem(group.Name, ExplorerItemKind.Category, ExplorerIcon.Box)
                {
                    Children = group.Methods.Select(Operation).ToList(),
                };
            }
        }

        private ExplorerItem Operation(MethodModel method)
        {
            return new ExplorerItem(method.Name, ExplorerItemKind.Property, ExplorerIcon.StoredProc)
            {
                Children = method.Parameters.Where(p => !p.IsConstant).Select(Parameter).ToList(),
            };
        }

        private ExplorerItem Parameter(ParameterModel model)
        {
            return new ExplorerItem($"{model.Name}: {model.Type}", ExplorerItemKind.Parameter, ExplorerIcon.Parameter);
        }

        private void BuildAssembly(List<CodeFile> code, AssemblyName name)
        {
            string[] frameworkAssemblies =
#if NETCORE
                GetCoreFxReferenceAssemblies();
#else
            new[]
            {
                typeof(int).Assembly.Location,
                typeof(Uri).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
            };
#endif

            var assembliesToReference = frameworkAssemblies.Concat(new[]
            {
                typeof(Azure.Core.Request).Assembly.Location,
                typeof(JObject).Assembly.Location,
                LoadAssemblySafely("Microsoft.Bcl.AsyncInterfaces.dll").Location,
                typeof(SwaggerDataContextDriver).Assembly.Location,
            });

            var compilation = CSharpCompilation.Create(
                name.Name,
                code.Select(
                    f => CSharpSyntaxTree.ParseText(f.Contents, new CSharpParseOptions(LanguageVersion.CSharp8), f.Path)),
                assembliesToReference.Select(path => MetadataReference.CreateFromFile(path)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var fileStream = new FileStream(name.CodeBase, FileMode.Create))
            {
                var result = compilation.Emit(fileStream);
                var importantDiagnostics = result.Diagnostics.Where(d => !IsIgnored(d)).ToList();
                if (importantDiagnostics.Any())
                {
                    throw new Exception(
                        "Cannot compile typed context:\n" + string.Join(
                            "\n",
                            importantDiagnostics.Select(d => d.ToString())));
                }
            }
        }

        private bool IsIgnored(Diagnostic diagnostic)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Hidden)
            {
                return true;
            }

            if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                return diagnostic.Id switch
                {
                    "CS1701" => true,
                    "CS1702" => true,
                    _ => false,
                };
            }

            return false;
        }

        private static async Task<ServiceClientModel> GetModelAsync(string uri, GeneratorOptions options)
        {
            var (diag, doc) = await GetSwaggerDocument(uri);

            var generator = new ServiceClientModelFactory(options);
            return generator.Create(doc);
        }

        private static async Task<(OpenApiDiagnostic, OpenApiDocument)> GetSwaggerDocument(string input)
        {
            using (var client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
            {
                using (Stream docStream = await client.GetStreamAsync(input))
                {
                    var doc = ServiceClientModelFactory.ReadDocument(docStream, out OpenApiDiagnostic diagnostic);

                    return (diagnostic, doc);
                }
            }
        }
    }
}
