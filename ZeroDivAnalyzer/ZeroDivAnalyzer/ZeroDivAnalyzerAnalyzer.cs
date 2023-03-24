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
            BinaryExpressionSyntax divideExpression = context.Node as BinaryExpressionSyntax;
            ExpressionSyntax divisorNode = divideExpression.Right;
            if (divisorNode.Kind() == SyntaxKind.ParenthesizedExpression)
            {
                divisorNode = (ExpressionSyntax)divisorNode.ChildNodes().First();
            }

            var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation(), divisorNode.ToString());

            switch (divisorNode.Kind())
            {
                case SyntaxKind.NumericLiteralExpression:
                    if (IsLiteralZero(divisorNode))
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
                    var leftNode = ((BinaryExpressionSyntax)divisorNode).Left;
                    var rightNode = ((BinaryExpressionSyntax)divisorNode).Right;
                    bool error = false;
                    if (leftNode.Kind() == SyntaxKind.NumericLiteralExpression)
                    {
                        error = leftNode.ToString().Equals(rightNode.ToString());
                    }
                    if (leftNode.Kind() == SyntaxKind.IdentifierName)
                    {
                        var leftSymbol = context.SemanticModel.GetSymbolInfo(leftNode).Symbol;
                        var rightSymbol = context.SemanticModel.GetSymbolInfo(rightNode).Symbol;
                        if (leftSymbol.Kind == SymbolKind.Local || leftSymbol.Kind == SymbolKind.Field)
                        {
                            error = leftSymbol.Equals(rightSymbol, SymbolEqualityComparer.Default);
                        }
                    }

                    if (error)
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;

                case SyntaxKind.MultiplyExpression:
                    leftNode = ((BinaryExpressionSyntax)divisorNode).Left;
                    rightNode = ((BinaryExpressionSyntax)divisorNode).Right;
                    error = leftNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(leftNode)
                            || rightNode.Kind() == SyntaxKind.NumericLiteralExpression && IsLiteralZero(rightNode)
                            || leftNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, leftNode)
                            || rightNode.Kind() == SyntaxKind.IdentifierName && IsVarZero(context, rightNode);

                    if (error)
                    {
                        context.ReportDiagnostic(diagnostic);
                        Console.WriteLine(diagnostic.ToString());
                    }
                    break;
            }
        }

        private static bool IsLiteralZero(ExpressionSyntax divisor)
        {
            SyntaxToken token = divisor.ChildTokens().First();
            return token.Kind() == SyntaxKind.NumericLiteralToken && token.Value.ToString() == "0";
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
                    DataFlowAnalysis dataFlowAnalysis = context.SemanticModel.AnalyzeDataFlow(declarationParent, divisorParent);

                    var declaratorExpressionSyntax = declarationNode.Initializer.Value;
                    if (declaratorExpressionSyntax.Kind() == SyntaxKind.NumericLiteralExpression
                        && Int32.Parse(declaratorExpressionSyntax.ChildTokens().First().ValueText) == 0
                        && !dataFlowAnalysis.WrittenInside.Contains(variable))
                    {
                        return true;
                    }
                }
                catch (ArgumentException) { return false; }
            }
            return false;
        }
    }
}
