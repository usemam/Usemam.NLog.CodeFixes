using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Usemam.NLog.CodeFixes.Common;

namespace Usemam.NLog.CodeFixes
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ObsoleteLoggerMethodUsageAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ObsoleteLoggerMethodUsage";

        private const string Category = "Maintainability";

        private const string Title = "Obsolete method usage";

        private const string MessageFormat = "Obsolete method usage";

        private const string Description = "Method was marked as obsolete, thus its usage is deprecated";

        private static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            foreach (string methodName in Constants.MethodNames)
            {
                context.RegisterSyntaxNodeAction(
                    c => AnalyzeInvocationExpression(c, methodName), SyntaxKind.InvocationExpression);
            }

            foreach (string methodName in Constants.ExceptionMethodNames)
            {
                context.RegisterSyntaxNodeAction(
                    c => AnalyzeInvocationExpression(c, methodName), SyntaxKind.InvocationExpression);
            }
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, string methodName)
        {
            var syntax = (InvocationExpressionSyntax)context.Node;
            var model = context.SemanticModel;

            if (syntax.IsObsoleteLoggerMethodInvocation(model, methodName))
            {
                var diagnostic = Diagnostic.Create(Rule, syntax.GetLocation(), methodName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
