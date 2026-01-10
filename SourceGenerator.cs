using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text;

namespace LiteNetLibToFishNetSerializerSourceGenerator
{
    [Generator]
    public class SourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var codes = GenerateCodes(context.Compilation);

            //context.AddSource();
        }

        private static string GenerateCodes(Compilation compilation)
        {
            var interfaceSymbol = compilation.GetTypeByMetadataName("INetSerializable");
            if (interfaceSymbol == null)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("// This one is auto-generated`");
            sb.AppendLine("using FishNet.Serializing;");
            sb.AppendLine();
            sb.AppendLine($@"
namespace FishNet.Insthync.Serializing
{{
    public static class DataSerializer
    {{");
            foreach (var tree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(tree);

                var nodes = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<TypeDeclarationSyntax>();

                foreach (var node in nodes)
                {
                    var symbol = model.GetDeclaredSymbol(node) as INamedTypeSymbol;
                    if (symbol == null)
                        continue;

                    if (!symbol.AllInterfaces.Contains(interfaceSymbol))
                        continue;

                    // ===== WRITE =====
                    var serializeMethod = symbol.GetMembers("Serialize")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault();

                    var serializeSyntax = serializeMethod?
                        .DeclaringSyntaxReferences.FirstOrDefault()?
                        .GetSyntax() as MethodDeclarationSyntax;

                    if (serializeSyntax?.Body != null)
                    {
                        sb.Append($@"
        public static void Write{symbol.Name}(this Writer writer, {symbol.Name} data)
        {{");
                        var rewriterS = new WriterSyntaxRewriter(model);
                        foreach (var stmt in serializeSyntax.Body.Statements)
                        {
                            var rewritten = rewriterS.Visit(stmt);
                            sb.Append($@"
            {rewritten.ToFullString().Trim()}");
                        }

                        sb.AppendLine($@"
        }}");
                    }

                    // ===== READ =====
                    var deserializeMethod = symbol.GetMembers("Deserialize")
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault();

                    var deserializeSyntax = deserializeMethod?
                        .DeclaringSyntaxReferences.FirstOrDefault()?
                        .GetSyntax() as MethodDeclarationSyntax;

                    if (deserializeSyntax?.Body != null)
                    {
                        sb.Append($@"
        public static {symbol.Name} Read{symbol.Name}(this Reader reader)
        {{
            {symbol.Name} data = new {symbol.Name}();");
                        var rewriterD = new ReaderSyntaxRewriter(model);
                        foreach (var stmt in deserializeSyntax.Body.Statements)
                        {
                            var rewritten = rewriterD.Visit(stmt);
                            sb.Append($@"
            {rewritten.ToFullString().Trim()}");
                        }

                        sb.AppendLine($@"
            return data;
        }}");
                    }
                }
            }

            sb.Append($@"
    }}
}}");
            return sb.ToString();
        }

        public void Initialize(GeneratorInitializationContext context) { }
    }
}
