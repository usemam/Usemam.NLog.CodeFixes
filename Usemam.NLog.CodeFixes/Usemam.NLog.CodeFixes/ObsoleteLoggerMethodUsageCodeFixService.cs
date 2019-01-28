using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Usemam.NLog.CodeFixes.Common;

namespace Usemam.NLog.CodeFixes
{
    public class ObsoleteLoggerMethodUsageCodeFixService
    {
        public SyntaxNode ApplyFix(SyntaxNode root, InvocationExpressionSyntax syntax)
        {
            var invocation = GetInvocationReplacement(syntax);
            if (TryGetStringVarDeclaration(syntax, out var stringVarDeclaration))
            {
                var wrapper = SyntaxFactory
                    .Block(
                        stringVarDeclaration,
                        SyntaxFactory.ExpressionStatement(invocation))
                    .WithOpenBraceToken(SyntaxFactory.MissingToken(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(SyntaxFactory.MissingToken(SyntaxKind.CloseBraceToken));
                return root.ReplaceNode(syntax.Parent, wrapper.Statements);
            }

            return root.ReplaceNode(syntax, invocation);
        }

        private InvocationExpressionSyntax GetInvocationReplacement(InvocationExpressionSyntax syntax)
        {
            var stringArg = syntax.ArgumentList.Arguments[0];
            var exceptionArg = syntax.ArgumentList.Arguments[1];

            if (stringArg.CheckIfMultiline())
            {
                stringArg = SyntaxFactory.Argument(
                    SyntaxFactory.IdentifierName(GetStringVarName(syntax)));
            }

            var newArgs = SyntaxFactory.ArgumentList(
                syntax.ArgumentList.OpenParenToken,
                SyntaxFactory.SeparatedList(new[] { exceptionArg, stringArg }),
                syntax.ArgumentList.CloseParenToken);
            var oldMemberAccess = (MemberAccessExpressionSyntax)syntax.Expression;
            int methodIndex = Array.IndexOf(Constants.ExceptionMethodNames, oldMemberAccess.Name.Identifier.Text);
            InvocationExpressionSyntax invocation;
            if (methodIndex > -1)
            {
                var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    oldMemberAccess.Expression,
                    SyntaxFactory.IdentifierName(Constants.MethodNames[methodIndex]));
                invocation = SyntaxFactory.InvocationExpression(newMemberAccess, newArgs);
            }
            else
            {
                invocation = SyntaxFactory.InvocationExpression(syntax.Expression, newArgs);
            }

            return invocation
                .WithLeadingTrivia(syntax.GetLeadingTrivia())
                .WithTrailingTrivia(syntax.GetTrailingTrivia());
        }

        private bool TryGetStringVarDeclaration(InvocationExpressionSyntax syntax, out LocalDeclarationStatementSyntax declaration)
        {
            var stringArg = syntax.ArgumentList.Arguments[0];
            declaration = null;
            if (stringArg.CheckIfMultiline())
            {
                var declarator = SyntaxFactory
                    .VariableDeclarator(GetStringVarName(syntax))
                    .WithInitializer(SyntaxFactory.EqualsValueClause(stringArg.Expression));
                declaration =
                    SyntaxFactory
                        .LocalDeclarationStatement(
                            SyntaxFactory
                                .VariableDeclaration(
                                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)))
                                .WithVariables(SyntaxFactory.SingletonSeparatedList(declarator)))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturn);
                return true;
            }

            return false;
        }

        private string GetStringVarName(InvocationExpressionSyntax syntax)
        {
            var memberAccess = (MemberAccessExpressionSyntax)syntax.Expression;
            string methodName = memberAccess.Name.Identifier.Text;
            return $"{methodName.First().ToString().ToLower() + methodName.Substring(1)}LogMessage";
        }
    }
}