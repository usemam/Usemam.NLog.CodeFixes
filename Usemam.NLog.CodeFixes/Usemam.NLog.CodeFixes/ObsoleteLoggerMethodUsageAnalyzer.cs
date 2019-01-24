using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
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

        private const string Category = "Naming";

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
                    c => AnalyzeInvocationExpressionExceptionMethod(c, methodName), SyntaxKind.InvocationExpression);
            }
        }

        private void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context, string methodName)
        {
            var syntax = (InvocationExpressionSyntax)context.Node;
            var model = context.SemanticModel;
            var loggerIfaceType = model.Compilation.GetTypeByMetadataName("NLog.ILogger");
            var loggerImplType = model.Compilation.GetTypeByMetadataName("NLog.Logger");
            if (model.GetSymbolInfo(syntax.Expression).Symbol is IMethodSymbol methodSymbol
                && (loggerIfaceType.Equals(methodSymbol.ReceiverType) || loggerImplType.Equals(methodSymbol.ReceiverType)))
            {
                var args = syntax.ArgumentList.Arguments;
                if (methodSymbol.Name == methodName && args.Count == 2)
                {
                    var firstArgType = model.GetTypeInfo(args[0].Expression).ConvertedType;
                    var secondArgType = model.GetTypeInfo(args[1].Expression).ConvertedType;
                    if (firstArgType != null
                        && secondArgType != null
                        && firstArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(string).FullName))
                        && secondArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName)))
                    {
                        var diagnostic = Diagnostic.Create(Rule, syntax.GetLocation(), methodSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private void AnalyzeInvocationExpressionExceptionMethod(SyntaxNodeAnalysisContext context, string methodName)
        {

        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            // TODO: Replace the following code with your own analysis, generating Diagnostic objects for any issues you find
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower))
            {
                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
