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

            var visitedNodes = new HashSet<SyntaxNode>();

            if (IsExpressionZero(context, divisorNode, visitedNodes))
            {
                var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation(), divisorNode.ToString());
                context.ReportDiagnostic(diagnostic);
                Console.WriteLine(diagnostic.ToString());
            }
        }

        private static bool IsExpressionZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor, HashSet<SyntaxNode> visitedNodes)
        {
            if (divisor.Kind() == SyntaxKind.ParenthesizedExpression)
            {
                divisor = (ExpressionSyntax)divisor.ChildNodes().First();
            }

            if (visitedNodes.Contains(divisor))
            {
                return false;
            }
            else
            {
                visitedNodes.Add(divisor);
            }

            switch (divisor.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    return IsLiteralZero(context, divisor);

                case SyntaxKind.IdentifierName:
                    return IsVarZero(context, divisor, visitedNodes);

                case SyntaxKind.SubtractExpression:
                    return IsSubExpressionZero(context, divisor, visitedNodes);

                case SyntaxKind.MultiplyExpression:
                    return IsMulExpressionZero(context, divisor, visitedNodes);

                case SyntaxKind.InvocationExpression:
                    ISymbol method = context.SemanticModel.GetSymbolInfo(((InvocationExpressionSyntax)divisor).Expression).Symbol;
                    if (method.DeclaringSyntaxReferences != null && method.Kind == SymbolKind.Method)
                    {
                        foreach (var returnStatment in ((MethodDeclarationSyntax)method.DeclaringSyntaxReferences.First().GetSyntax()).DescendantNodes().OfType<ReturnStatementSyntax>())
                        {
                            if (IsExpressionZero(context, returnStatment.Expression, visitedNodes))
                            {
                                return true;
                            }
                        }
                    }
                    break;
            }
            return false;
        }

        private static bool IsLiteralZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor)
        {
            var val = context.SemanticModel.GetConstantValue(divisor);
            return val.HasValue && val.Value.Equals(Convert.ChangeType(0, val.Value.GetType()));
        }

        private static bool IsVarZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor, HashSet<SyntaxNode> visitedNodes)
        {
            ISymbol variable = context.SemanticModel.GetSymbolInfo(divisor).Symbol;
            if (variable.DeclaringSyntaxReferences != null)
            {
                VariableDeclaratorSyntax declarationNode = variable.DeclaringSyntaxReferences.First().GetSyntax() as VariableDeclaratorSyntax;
                if (declarationNode == null)
                {
                    return false;
                }

                SyntaxNode declarationParent = declarationNode.GetLastToken().GetNextToken().GetNextToken().Parent.FirstAncestorOrSelf<StatementSyntax>();
                SyntaxNode divisorParent = divisor.FirstAncestorOrSelf<StatementSyntax>(syntax => syntax.Parent.Equals(declarationParent.Parent));

                try
                {
                    if (IsExpressionZero(context, declarationNode.Initializer.Value, visitedNodes)
                        && !context.SemanticModel.AnalyzeDataFlow(declarationParent, divisorParent).WrittenInside.Contains(variable))
                    {
                        return true;
                    }
                }
                catch (ArgumentException) { return false; }
            }
            return false;
        }

        private static bool IsSubExpressionZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor, HashSet<SyntaxNode> visitedNodes)
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

        private static bool IsMulExpressionZero(SyntaxNodeAnalysisContext context, ExpressionSyntax divisor, HashSet<SyntaxNode> visitedNodes)
        {
            var leftNode = ((BinaryExpressionSyntax)divisor).Left;
            var rightNode = ((BinaryExpressionSyntax)divisor).Right;
            return leftNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(context, leftNode)
                    || rightNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(context, rightNode)
                    || leftNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, leftNode, visitedNodes)
                    || rightNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, rightNode, visitedNodes);
        }
    }
}
