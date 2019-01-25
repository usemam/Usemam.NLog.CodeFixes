using System;
using System.Collections.Immutable;
using System.Composition;
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
                        if (syntax.IsObsoleteLoggerMethodInvocation(model, Constants.MethodNames))
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    Title,
                                    _ => GetTransformedDocumentAsync(context.Document, root, syntax, GetMethodReplacement),
                                    nameof(ObsoleteLoggerMethodUsageCodeFixProvider)),
                                diagnostic);
                        }
                        else if (syntax.IsObsoleteLoggerMethodInvocation(model, Constants.ExceptionMethodNames))
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    Title,
                                    _ => GetTransformedDocumentAsync(context.Document, root, syntax, GetExceptionMethodReplacement),
                                    nameof(ObsoleteLoggerMethodUsageCodeFixProvider)),
                                diagnostic);
                        }
                    }
                }
            }
        }

        private Task<Document> GetTransformedDocumentAsync(
            Document document, SyntaxNode root, InvocationExpressionSyntax syntax, Func<InvocationExpressionSyntax, SyntaxNode> substitute)
        {
            var replacement = substitute(syntax);
            var newRoot = root.ReplaceNode(syntax, replacement);
            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        private SyntaxNode GetMethodReplacement(InvocationExpressionSyntax syntax)
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

        private SyntaxNode GetExceptionMethodReplacement(InvocationExpressionSyntax syntax)
        {
            var firstArg = syntax.ArgumentList.Arguments[0];
            var secondArg = syntax.ArgumentList.Arguments[1];
            var newArgs = SyntaxFactory.ArgumentList(
                syntax.ArgumentList.OpenParenToken,
                SyntaxFactory.SeparatedList(new[] { secondArg, firstArg }),
                syntax.ArgumentList.CloseParenToken);
            var oldMemberAccess = (MemberAccessExpressionSyntax) syntax.Expression;
            int methodIndex = Array.IndexOf(Constants.ExceptionMethodNames, oldMemberAccess.Name.Identifier.Text);
            var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                oldMemberAccess.Expression,
                SyntaxFactory.IdentifierName(Constants.MethodNames[methodIndex]));
            return SyntaxFactory.InvocationExpression(newMemberAccess, newArgs)
                .WithLeadingTrivia(syntax.GetLeadingTrivia())
                .WithTrailingTrivia(syntax.GetTrailingTrivia());
        }
    }
}
