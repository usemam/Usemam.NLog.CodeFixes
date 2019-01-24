using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Usemam.NLog.CodeFixes.Common;

namespace Usemam.NLog.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ObsoleteLoggerMethodUsageCodeFixProvider)), Shared]
    public class ObsoleteLoggerMethodUsageCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Use override";

        private const string ObsoleteMethodUsageDiagnosticId = "CS0618";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(ObsoleteMethodUsageDiagnosticId, ObsoleteLoggerMethodUsageAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                if (diagnostic.Id != ObsoleteMethodUsageDiagnosticId
                    && diagnostic.Id != ObsoleteLoggerMethodUsageAnalyzer.DiagnosticId)
                {
                    continue;
                }

                SyntaxNode node = root.FindNode(
                    diagnostic.Location.SourceSpan, getInnermostNodeForTie: true, findInsideTrivia: true);
                if (node.IsMissing)
                {
                    continue;
                }

                if (node is InvocationExpressionSyntax syntax)
                {
                    if (context.Document.TryGetSemanticModel(out var model))
                    {
                        var loggerIfaceType = model.Compilation.GetTypeByMetadataName("NLog.ILogger");
                        var loggerImplType = model.Compilation.GetTypeByMetadataName("NLog.Logger");

                        if (model.GetSymbolInfo(syntax.Expression).Symbol is IMethodSymbol methodSymbol
                            && (loggerIfaceType.Equals(methodSymbol.ReceiverType) || loggerImplType.Equals(methodSymbol.ReceiverType)))
                        {
                            var args = syntax.ArgumentList.Arguments;
                            if (Constants.MethodNames.Contains(methodSymbol.Name) && args.Count == 2)
                            {
                                var firstArgType = model.GetTypeInfo(args[0].Expression).ConvertedType;
                                var secondArgType = model.GetTypeInfo(args[1].Expression).ConvertedType;
                                if (firstArgType != null
                                    && secondArgType != null
                                    && firstArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(string).FullName))
                                    && secondArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName)))
                                {
                                    context.RegisterCodeFix(
                                        CodeAction.Create(
                                            Title,
                                            _ => GetTransformedDocumentAsync(context.Document, root, syntax),
                                            nameof(ObsoleteLoggerMethodUsageCodeFixProvider)),
                                        diagnostic);
                                }
                            }
                            else if (Constants.ExceptionMethodNames.Contains(methodSymbol.Name))
                            {
                                // todo
                            }
                        }
                    }
                }
            }
        }

        private Task<Document> GetTransformedDocumentAsync(Document document, SyntaxNode root, InvocationExpressionSyntax syntax)
        {
            var replacement = GetReplacement(syntax);
            var newRoot = root.ReplaceNode(syntax, replacement);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private SyntaxNode GetReplacement(InvocationExpressionSyntax syntax)
        {
            var firstArg = syntax.ArgumentList.Arguments[0];
            var secondArg = syntax.ArgumentList.Arguments[1];
            var newArgs = SyntaxFactory.ArgumentList(
                syntax.ArgumentList.OpenParenToken,
                SyntaxFactory.SeparatedList(new[] { secondArg, firstArg }),
                syntax.ArgumentList.CloseParenToken);
            return SyntaxFactory.InvocationExpression(syntax.Expression, newArgs)
                .WithLeadingTrivia(syntax.GetLeadingTrivia())
                .WithTrailingTrivia(syntax.GetTrailingTrivia());
        }
    }
}
