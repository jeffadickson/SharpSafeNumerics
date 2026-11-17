using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharpSafeNumerics.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StrictNumericAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor UncheckedRule = new(
        "SSN1002", "Unchecked arithmetic is forbidden in a strict numeric scope",
        "Unchecked arithmetic is forbidden inside StrictNumeric scope",
        "SharpSafeNumerics.Safety", DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor IgnoredResultRule = new(
        "SSN1003", "Numeric result is ignored",
        "NumericResult<T> return value is ignored; inspect success or failure explicitly",
        "SharpSafeNumerics.Safety", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor ThrowPolicyRule = new(
        "SSN1004", "Throw policy is forbidden in a strict numeric scope",
        "ArithmeticPolicy.Throw is forbidden inside StrictNumeric scope",
        "SharpSafeNumerics.Safety", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UncheckedRule, IgnoredResultRule, ThrowPolicyRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeUnchecked, SyntaxKind.UncheckedExpression, SyntaxKind.UncheckedStatement);
        context.RegisterSyntaxNodeAction(AnalyzeThrowPolicy, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIgnoredResult, SyntaxKind.ExpressionStatement);
    }

    private static void AnalyzeUnchecked(SyntaxNodeAnalysisContext context)
    {
        if (IsStrict(context.Node, context.SemanticModel))
            context.ReportDiagnostic(Diagnostic.Create(UncheckedRule, context.Node.GetLocation()));
    }

    private static void AnalyzeThrowPolicy(SyntaxNodeAnalysisContext context)
    {
        if (!IsStrict(context.Node, context.SemanticModel))
            return;
        var symbol = context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol;
        if (symbol is IFieldSymbol { Name: "Throw" } field &&
            field.ContainingType.ToDisplayString() == "SharpSafeNumerics.ArithmeticPolicy")
            context.ReportDiagnostic(Diagnostic.Create(ThrowPolicyRule, context.Node.GetLocation()));
    }

    private static void AnalyzeIgnoredResult(SyntaxNodeAnalysisContext context)
    {
        var expression = ((ExpressionStatementSyntax)context.Node).Expression;
        var type = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type as INamedTypeSymbol;
        if (type is { Name: "NumericResult", IsGenericType: true } &&
            type.ContainingNamespace.ToDisplayString() == "SharpSafeNumerics")
            context.ReportDiagnostic(Diagnostic.Create(IgnoredResultRule, expression.GetLocation()));
    }

    private static bool IsStrict(SyntaxNode node, SemanticModel model)
    {
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            ISymbol? symbol = ancestor switch
            {
                MethodDeclarationSyntax m => model.GetDeclaredSymbol(m),
                StructDeclarationSyntax s => model.GetDeclaredSymbol(s),
                ClassDeclarationSyntax c => model.GetDeclaredSymbol(c),
                _ => null
            };
            if (symbol is null) continue;
            if (symbol.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "SharpSafeNumerics.StrictNumericAttribute"))
                return true;
        }
        return false;
    }
}
