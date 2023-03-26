using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;


namespace ZeroDivAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ZeroDivAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ZeroDivAnalyzer";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Using";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.DivideExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            ExpressionSyntax divisorNode = ((BinaryExpressionSyntax)context.Node).Right;

            if (divisorNode.Kind() == SyntaxKind.ParenthesizedExpression)
            {
                divisorNode = (ExpressionSyntax)divisorNode.ChildNodes().First();
            }

            var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation(), divisorNode.ToString());

            if (divisorNode.Kind() == SyntaxKind.InvocationExpression)
            {
                ISymbol method = context.SemanticModel.GetSymbolInfo(((InvocationExpressionSyntax)divisorNode).Expression).Symbol;
                if (method.DeclaringSyntaxReferences != null && method.Kind == SymbolKind.Method)
                {
                    divisorNode = ((MethodDeclarationSyntax)method.DeclaringSyntaxReferences.First().GetSyntax()).DescendantNodes().OfType<ReturnStatementSyntax>().FirstOrDefault().Expression;
                }
            }


            switch (divisorNode.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    if (IsLiteralZero(context, divisorNode))
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;

                case SyntaxKind.IdentifierName:
                    if (IsVarZero(context, divisorNode))
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;

                case SyntaxKind.SubtractExpression:
                    if (IsSubExpressionZero(context, divisorNode))
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;

                case SyntaxKind.MultiplyExpression:
                    if (IsMulExpressionZero(context, divisorNode))
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;
            }
        }

        private static bool IsLiteralZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor)
        {
            return divisor.Kind() == SyntaxKind.NumericLiteralExpression && (int)context.SemanticModel.GetConstantValue(divisor).Value == 0;
        }

        private static bool IsVarZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor)
        {
            ISymbol variable = context.SemanticModel.GetSymbolInfo(divisor).Symbol;
            
            if (variable.DeclaringSyntaxReferences != null && variable.Kind == SymbolKind.Local)
            {
                VariableDeclaratorSyntax declarationNode = variable.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax;

                SyntaxNode declarationParent = declarationNode.GetLastToken().GetNextToken().GetNextToken().Parent;
                SyntaxNode divisorParent = divisor;
                while (!(declarationParent is StatementSyntax))
                {
                    declarationParent = declarationParent.Parent;
                }
                while (!(divisorParent is StatementSyntax))
                {
                    divisorParent = divisorParent.Parent;
                }

                try
                {
                    var declaratorExpressionSyntax = declarationNode.Initializer.Value;

                    if (declaratorExpressionSyntax.Kind() == SyntaxKind.NumericLiteralExpression
                        && (int)context.SemanticModel.GetConstantValue(declaratorExpressionSyntax).Value == 0
                        && !context.SemanticModel.AnalyzeDataFlow(declarationParent, divisorParent).WrittenInside.Contains(variable))
                    {
                        return true;
                    }
                }
                catch (ArgumentException) { return false; }
            }
            return false;
        }

        private static bool IsSubExpressionZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor)
        {
            var leftNode = ((BinaryExpressionSyntax)divisor).Left;
            var rightNode = ((BinaryExpressionSyntax)divisor).Right;
            if (leftNode.Kind() == SyntaxKind.NumericLiteralExpression)
            {
                return leftNode.ToString().Equals(rightNode.ToString());
            }
            if (leftNode.Kind() == SyntaxKind.IdentifierName)
            {
                var leftSymbol = context.SemanticModel.GetSymbolInfo(leftNode).Symbol;
                if (leftSymbol.Kind == SymbolKind.Local || leftSymbol.Kind == SymbolKind.Field || leftSymbol.Kind == SymbolKind.Parameter)
                {
                    return leftSymbol.Equals(context.SemanticModel.GetSymbolInfo(rightNode).Symbol, SymbolEqualityComparer.Default);
                }
            }
            return false;
        }

        private static bool IsMulExpressionZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor)
        {
            var leftNode = ((BinaryExpressionSyntax)divisor).Left;
            var rightNode = ((BinaryExpressionSyntax)divisor).Right;
            return leftNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(context, leftNode)
                    || rightNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(context, rightNode)
                    || leftNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, leftNode)
                    || rightNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, rightNode);
        }
    }
}
