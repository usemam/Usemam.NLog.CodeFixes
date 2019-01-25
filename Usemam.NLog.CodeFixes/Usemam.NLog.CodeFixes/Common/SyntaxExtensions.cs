using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Usemam.NLog.CodeFixes.Common
{
    internal static class SyntaxExtensions
    {
        public static bool IsObsoleteLoggerMethodInvocation(
            this InvocationExpressionSyntax syntax, SemanticModel model, params string[] methodNames)
        {
            var loggerIfaceType = model.Compilation.GetTypeByMetadataName("NLog.ILogger");
            var loggerImplType = model.Compilation.GetTypeByMetadataName("NLog.Logger");

            if (model.GetSymbolInfo(syntax.Expression).Symbol is IMethodSymbol methodSymbol
                && (loggerIfaceType.Equals(methodSymbol.ReceiverType) ||
                    loggerImplType.Equals(methodSymbol.ReceiverType)))
            {
                var args = syntax.ArgumentList.Arguments;
                if (methodNames.Contains(methodSymbol.Name) && args.Count == 2)
                {
                    var firstArgType = model.GetTypeInfo(args[0].Expression).ConvertedType;
                    var secondArgType = model.GetTypeInfo(args[1].Expression).ConvertedType;
                    if (firstArgType != null
                        && secondArgType != null
                        && firstArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(string).FullName))
                        && secondArgType.Equals(model.Compilation.GetTypeByMetadataName(typeof(Exception).FullName)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}