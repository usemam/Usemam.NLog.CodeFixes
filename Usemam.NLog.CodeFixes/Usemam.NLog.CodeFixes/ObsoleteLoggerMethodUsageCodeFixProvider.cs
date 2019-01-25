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
            return new FixAll();
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
                        if (syntax.IsObsoleteLoggerMethodInvocation(
                            model, Constants.MethodNames.Concat(Constants.ExceptionMethodNames).ToArray()))
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    Title,
                                    _ => GetTransformedDocumentAsync(context.Document, root, syntax),
                                    nameof(ObsoleteLoggerMethodUsageCodeFixProvider)),
                                diagnostic);
                        }
                    }
                }
            }
        }

        private Task<Document> GetTransformedDocumentAsync(
            Document document, SyntaxNode root, InvocationExpressionSyntax syntax)
        {
            var replacement = GetMethodReplacement(syntax);
            var newRoot = root.ReplaceNode(syntax, replacement);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private static SyntaxNode GetMethodReplacement(InvocationExpressionSyntax syntax)
        {
            var firstArg = syntax.ArgumentList.Arguments[0];
            var secondArg = syntax.ArgumentList.Arguments[1];
            var newArgs = SyntaxFactory.ArgumentList(
                syntax.ArgumentList.OpenParenToken,
                SyntaxFactory.SeparatedList(new[] { secondArg, firstArg }),
                syntax.ArgumentList.CloseParenToken);
            var oldMemberAccess = (MemberAccessExpressionSyntax) syntax.Expression;
            int methodIndex = Array.IndexOf(Constants.ExceptionMethodNames, oldMemberAccess.Name.Identifier.Text);
            if (methodIndex > -1)
            {
                var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    oldMemberAccess.Expression,
                    SyntaxFactory.IdentifierName(Constants.MethodNames[methodIndex]));
                return SyntaxFactory.InvocationExpression(newMemberAccess, newArgs)
                    .WithLeadingTrivia(syntax.GetLeadingTrivia())
                    .WithTrailingTrivia(syntax.GetTrailingTrivia());
            }

            return SyntaxFactory.InvocationExpression(syntax.Expression, newArgs)
                .WithLeadingTrivia(syntax.GetLeadingTrivia())
                .WithTrailingTrivia(syntax.GetTrailingTrivia());
        }

        private class FixAll : FixAllInDocumentProvider
        {
            protected override string ActionTitle => Title;

            /// <summary>
            /// Fixes all occurrences of a diagnostic in a specific document.
            /// </summary>
            /// <param name="context">The context for the Fix All operation.</param>
            /// <param name="document">The document to fix.</param>
            /// <param name="diagnostics">The diagnostics to fix in the document.</param>
            protected override async Task<SyntaxNode> FixAllInDocumentAsync(
                FixAllContext context, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                if (diagnostics.IsEmpty)
                {
                    return null;
                }

                var syntaxRoot = await document.GetSyntaxRootAsync().ConfigureAwait(false);
                var nodes = diagnostics.Select(
                    diagnostic => syntaxRoot.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true, findInsideTrivia: true));
                return syntaxRoot.ReplaceNodes(
                    nodes,
                    (_, newNode) => GetMethodReplacement((InvocationExpressionSyntax) newNode));
            }
        }
    }
}
