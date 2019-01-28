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

            var symbolInfo = model.GetSymbolInfo(syntax.Expression);
            IMethodSymbol methodSymbol = null;
            if (symbolInfo.Symbol != null)
            {
                methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            }
            else if (symbolInfo.CandidateSymbols.Length > 0)
            {
                methodSymbol = symbolInfo.CandidateSymbols[0] as IMethodSymbol;
            }

            if (methodSymbol != null &&
                (loggerIfaceType.Equals(methodSymbol.ReceiverType) || loggerImplType.Equals(methodSymbol.ReceiverType)))
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

        public static bool CheckIfMultiline(this ArgumentSyntax argument)
        {
            var lineSpan = argument.SyntaxTree.GetLineSpan(argument.Span);
            return lineSpan.EndLinePosition.Line > lineSpan.StartLinePosition.Line;
        }
    }
}