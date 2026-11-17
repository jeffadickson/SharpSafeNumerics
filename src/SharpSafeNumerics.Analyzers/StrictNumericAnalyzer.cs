using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SharpSafeNumerics.Analyzers;

/// <summary>Reports detectable numeric safety violations in C# consumer code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StrictNumericAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "SharpSafeNumerics.Safety";

    private static readonly DiagnosticDescriptor NarrowingRule = new(
        "SSN1001", "Narrowing integer conversion in a strict numeric scope",
        "Conversion from '{0}' to '{1}' can lose range or throw; use an exact status-returning conversion",
        Category, DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor UncheckedRule = new(
        "SSN1002", "Unchecked arithmetic is forbidden in a strict numeric scope",
        "Unchecked arithmetic is forbidden inside StrictNumeric scope",
        Category, DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor IgnoredResultRule = new(
        "SSN1003", "Numeric result is ignored",
        "NumericResult<T> value is discarded; inspect or propagate it",
        Category, DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor ThrowPolicyRule = new(
        "SSN1004", "Throw policy is forbidden in a strict numeric scope",
        "ArithmeticPolicy.Throw is forbidden inside StrictNumeric scope",
        Category, DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor RawPolicyConversionRule = new(
        "SSN1005", "Raw saturating or truncating conversion in a strict numeric scope",
        "'{0}' bypasses the named numeric policy boundary; use an exact status-returning conversion or an approved policy helper",
        Category, DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor DomainBypassRule = new(
        "SSN1006", "Bounded domain admission or status handling is bypassed",
        "'{0}' bypasses bounded range admission or status handling; use the generated Try API",
        Category, DiagnosticSeverity.Error, true);

    private static readonly DiagnosticDescriptor AllocationRule = new(
        "SSN1007", "Potential managed allocation in a strict numeric scope",
        "Potential managed allocation in StrictNumeric scope: {0}",
        Category, DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor InvalidDefaultRule = new(
        "SSN1008", "Default value of this bounded domain is invalid",
        "Default construction of '{0}' produces an invalid value; use the generated TryCreate API",
        Category, DiagnosticSeverity.Error, true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(NarrowingRule, UncheckedRule, IgnoredResultRule, ThrowPolicyRule,
            RawPolicyConversionRule, DomainBypassRule, AllocationRule, InvalidDefaultRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var contracts = new NumericSymbols(start.Compilation);
            if (contracts.Result is not null)
            {
                start.RegisterOperationAction(c => AnalyzeIgnoredResult(c, contracts),
                    OperationKind.ExpressionStatement, OperationKind.SimpleAssignment, OperationKind.Discard);
            }

            if (contracts.Strict is null)
                return;

            // Roslyn represents checked/unchecked wrappers through flags on child operations.
            // Preserve an explicit unchecked override as its own diagnostic, even for constant code.
            start.RegisterSyntaxNodeAction(c =>
            {
                if (contracts.IsStrict(c.ContainingSymbol) ||
                    contracts.IsStrict(c.SemanticModel.GetEnclosingSymbol(c.Node.SpanStart, c.CancellationToken)))
                {
                    c.ReportDiagnostic(Diagnostic.Create(UncheckedRule, c.Node.GetLocation()));
                }
            }, SyntaxKind.UncheckedExpression, SyntaxKind.UncheckedStatement);

            start.RegisterOperationAction(c => AnalyzeStrict(c, contracts),
                OperationKind.Conversion, OperationKind.BinaryOperator, OperationKind.UnaryOperator,
                OperationKind.CompoundAssignment, OperationKind.Increment, OperationKind.Decrement,
                OperationKind.Invocation, OperationKind.Argument, OperationKind.FieldReference,
                OperationKind.LocalReference, OperationKind.PropertyReference, OperationKind.Conditional,
                OperationKind.Literal, OperationKind.DefaultValue, OperationKind.ObjectCreation,
                OperationKind.TypeParameterObjectCreation, OperationKind.AnonymousObjectCreation,
                OperationKind.ArrayCreation, OperationKind.CollectionExpression,
                OperationKind.InterpolatedString, OperationKind.AnonymousFunction, OperationKind.DelegateCreation);
        });
    }

    private static void AnalyzeIgnoredResult(OperationAnalysisContext context, NumericSymbols contracts)
    {
        IOperation? discarded = context.Operation switch
        {
            IExpressionStatementOperation { Operation: not IAssignmentOperation } statement => statement.Operation,
            ISimpleAssignmentOperation { Target: IDiscardOperation } assignment => assignment.Value,
            IDiscardOperation discard when discard.Parent is IArgumentOperation or IDeclarationExpressionOperation
                or ITupleOperation => discard,
            _ => null
        };
        if (discarded is not null && contracts.IsNumericResult(discarded.Type))
            Report(context, IgnoredResultRule, discarded);
    }

    private static void AnalyzeStrict(OperationAnalysisContext context, NumericSymbols contracts)
    {
        var operation = context.Operation;
        // Parent operations retain the lexical function symbol even when the action's containing symbol
        // is the outer operation-block owner. A lambda's construction is evaluated by its caller.
        if (!contracts.IsStrict(context.ContainingSymbol) && !InsideStrictFunction(operation.Parent, contracts))
            return;

        if (!operation.IsImplicit && contracts.IsThrowConstant(operation) &&
            !operation.Descendants().Any(contracts.IsThrowConstant))
        {
            Report(context, ThrowPolicyRule, operation);
        }

        switch (operation)
        {
            case IConversionOperation conversion:
                AnalyzeConversion(context, contracts, conversion);
                break;
            case IInvocationOperation invocation:
                AnalyzeInvocation(context, contracts, invocation);
                break;
            case IArgumentOperation { ArgumentKind: ArgumentKind.DefaultValue } argument
                when contracts.IsThrowConstant(argument.Value):
                Report(context, ThrowPolicyRule, argument);
                break;
            case IFieldReferenceOperation field when
                field.Field.Name is "__ssnValue" or "__ssnValid" && contracts.IsBoundedGeneratedMember(field.Field) &&
                !InsideNameOf(field):
                Report(context, DomainBypassRule, field, field.Field.Name);
                break;
            case IPropertyReferenceOperation property when
                property.Property.Name == "Value" && contracts.IsBoundedGeneratedMember(property.Property) &&
                !contracts.IsZeroValid(property.Property.ContainingType) && !InsideNameOf(property):
                Report(context, DomainBypassRule, property, property.Property.Name);
                break;
            case IObjectCreationOperation creation:
                if (creation.Type?.IsReferenceType == true)
                    Report(context, AllocationRule, creation, "reference-type construction");
                else if (creation.Constructor?.IsImplicitlyDeclared == true)
                    AnalyzeDefault(context, contracts, creation);
                else if (creation.Constructor is { } constructor && contracts.IsBoundedGeneratedMember(constructor))
                    Report(context, DomainBypassRule, creation, constructor.ContainingType.Name);
                break;
            case ITypeParameterObjectCreationOperation { Type.IsReferenceType: true }:
                Report(context, AllocationRule, operation, "reference-constrained type construction");
                break;
            case IDefaultValueOperation { IsImplicit: false }:
                AnalyzeDefault(context, contracts, operation);
                break;
            case IAnonymousObjectCreationOperation:
                Report(context, AllocationRule, operation, "anonymous object construction");
                break;
            case IArrayCreationOperation array when !array.IsImplicit || array.Initializer?.ElementValues.Length > 0:
                // Empty implicit params arrays can use Array.Empty<T>().
                Report(context, AllocationRule, operation, "array construction");
                break;
            case ICollectionExpressionOperation { Type: IArrayTypeSymbol, Elements.Length: > 0 }:
                Report(context, AllocationRule, operation, "array collection expression");
                break;
            case IInterpolatedStringOperation text when !text.ConstantValue.HasValue && !IsHandlerContent(text) &&
                (text.Type?.SpecialType == SpecialType.System_String || contracts.IsFormattingObject(text.Type)):
                Report(context, AllocationRule, text, "interpolated string materialization");
                break;
            case IAnonymousFunctionOperation function when CapturesState(function, context.CancellationToken):
                Report(context, AllocationRule, function, "capturing lambda or anonymous method");
                break;
            case IDelegateCreationOperation creation when creation.Target is IMethodReferenceOperation method &&
                (!creation.IsImplicit || method.Instance is not null || method.Method.MethodKind == MethodKind.LocalFunction):
                Report(context, AllocationRule, creation, "delegate construction");
                break;
            default:
                AnalyzeArithmetic(context, contracts, operation);
                break;
        }
    }

    private static bool InsideStrictFunction(IOperation? operation, NumericSymbols contracts)
    {
        for (; operation is not null; operation = operation.Parent)
        {
            if (operation is ILocalFunctionOperation local && contracts.IsStrict(local.Symbol) ||
                operation is IAnonymousFunctionOperation lambda && contracts.IsStrict(lambda.Symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InsideNameOf(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is INameOfOperation)
                return true;
        }
        return false;
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, NumericSymbols contracts,
        IConversionOperation conversion)
    {
        if (contracts.IsNarrowing(conversion))
        {
            Report(context, NarrowingRule, conversion, conversion.Operand.Type!, conversion.Type!);
        }
        else if (conversion.OperatorMethod is { } method && contracts.IsBoundedGeneratedMember(method) &&
            (contracts.IsBounded(NumericSymbols.UnwrapNullable(conversion.Type)) ||
             !contracts.IsZeroValid(method.ContainingType)))
        {
            Report(context, DomainBypassRule, conversion, method.ContainingType.Name);
        }

        if (contracts.IsBoxing(conversion))
            Report(context, AllocationRule, conversion, "boxing");
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, NumericSymbols contracts,
        IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;
        if (contracts.IsRawNumericConversion(method))
            Report(context, RawPolicyConversionRule, invocation, method.Name);
        if (contracts.IsBoundedReinterpretation(method))
            Report(context, DomainBypassRule, invocation, method.Name);
        if (method.Name is "Add" or "Subtract" && contracts.IsBoundedGeneratedMember(method) &&
            contracts.HasThrowOverflowPolicy(method.ContainingType))
        {
            Report(context, ThrowPolicyRule, invocation);
        }
    }

    private static void AnalyzeDefault(OperationAnalysisContext context, NumericSymbols contracts, IOperation operation)
    {
        if (contracts.IsBounded(operation.Type) && !contracts.IsZeroValid(operation.Type))
            Report(context, InvalidDefaultRule, operation, operation.Type!);
    }

    private static void AnalyzeArithmetic(OperationAnalysisContext context, NumericSymbols contracts, IOperation operation)
    {
        // Divisions and shifts have different overflow semantics. These are the built-in operators
        // whose unchecked form can silently wrap, including the implicit narrowing in x += y.
        bool potentiallyWrapping = operation switch
        {
            IBinaryOperation binary => !binary.IsChecked && IsWrapping(binary.OperatorKind) &&
                contracts.IsBuiltinIntegerOperator(binary.OperatorMethod),
            ICompoundAssignmentOperation assignment => !assignment.IsChecked && IsWrapping(assignment.OperatorKind) &&
                contracts.IsBuiltinIntegerOperator(assignment.OperatorMethod),
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Minus } unary => !unary.IsChecked &&
                contracts.IsBuiltinIntegerOperator(unary.OperatorMethod),
            IIncrementOrDecrementOperation increment => !increment.IsChecked &&
                contracts.IsBuiltinIntegerOperator(increment.OperatorMethod),
            _ => false
        };
        if (potentiallyWrapping && !operation.ConstantValue.HasValue && contracts.IsInteger(operation.Type) &&
            !IsExplicitUncheckedUntilBoundary(operation.Syntax))
            Report(context, UncheckedRule, operation);
    }

    private static bool IsExplicitUncheckedUntilBoundary(SyntaxNode node)
    {
        // C# does not propagate checked contexts into lambda or local-function bodies.
        foreach (var ancestor in node.AncestorsAndSelf())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax ||
                ancestor.IsKind(SyntaxKind.CheckedExpression) || ancestor.IsKind(SyntaxKind.CheckedStatement))
            {
                return false;
            }
            if (ancestor.IsKind(SyntaxKind.UncheckedExpression) || ancestor.IsKind(SyntaxKind.UncheckedStatement))
                return true;
        }

        return false;
    }

    private static bool IsWrapping(BinaryOperatorKind kind) =>
        kind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply;

    private static bool IsHandlerContent(IInterpolatedStringOperation text)
    {
        // A handler may receive several interpolated strings joined by '+'. Roslyn gives those
        // contents an intermediate string type, although it does not materialize a string.
        for (var parent = text.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IInterpolatedStringHandlerCreationOperation)
                return true;
            if (parent is not IInterpolatedStringAdditionOperation)
                return false;
        }

        return false;
    }

    private static bool CapturesState(IAnonymousFunctionOperation function, CancellationToken cancellationToken) =>
        !function.Symbol.IsStatic && CapturesState(function.Body,
            new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default) { function.Symbol }, cancellationToken);

    private static bool CapturesState(IOperation body, HashSet<IMethodSymbol> activeFunctions,
        CancellationToken cancellationToken)
    {
        foreach (var child in body.ChildOperations)
        {
            // Declarations have their own actions. A call to a local function can still
            // require the enclosing delegate to capture that function's outer variables.
            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation or INameOfOperation)
                continue;

            var variable = child switch
            {
                ILocalReferenceOperation { Local.IsConst: false } local => (ISymbol)local.Local,
                IParameterReferenceOperation parameter => parameter.Parameter,
                _ => null
            };
            if (child is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } ||
                variable is not null && HasExternalOwner(variable, activeFunctions) ||
                CallsCapturingLocalFunction(child, activeFunctions, cancellationToken) ||
                CapturesState(child, activeFunctions, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExternalOwner(ISymbol variable, HashSet<IMethodSymbol> activeFunctions)
    {
        // A variable declared inside one of the active calls is created during that call.
        // It doesn't need to survive from delegate creation to the eventual invocation.
        for (var owner = variable.ContainingSymbol; owner is not null; owner = owner.ContainingSymbol)
        {
            if (owner is IMethodSymbol method && activeFunctions.Contains(method))
                return false;
        }
        return true;
    }

    private static bool CallsCapturingLocalFunction(IOperation operation, HashSet<IMethodSymbol> activeFunctions,
        CancellationToken cancellationToken)
    {
        var target = operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation reference => reference.Method,
            _ => null
        };
        if (target is not { MethodKind: MethodKind.LocalFunction, IsStatic: false } ||
            !activeFunctions.Add(target.OriginalDefinition))
        {
            return false;
        }

        // A local function binds in the same syntax tree. Use the semantic model from the
        // operation, and stop at cycles so recursive local functions terminate this check.
        var declaration = target.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken);
        bool captures = declaration is not null &&
            operation.SemanticModel?.GetOperation(declaration, cancellationToken) is ILocalFunctionOperation { Body: { } body } &&
            CapturesState(body, activeFunctions, cancellationToken);
        activeFunctions.Remove(target.OriginalDefinition);
        return captures;
    }

    private static void Report(OperationAnalysisContext context, DiagnosticDescriptor descriptor,
        IOperation operation, params object[] arguments)
    {
        // Attribute arguments become metadata. Roslyn's object construction and array operations
        // on an attribute are not allocations in the annotated application's execution path.
        if (descriptor == AllocationRule && operation.Syntax.FirstAncestorOrSelf<AttributeSyntax>() is not null)
            return;
        context.ReportDiagnostic(Diagnostic.Create(descriptor, operation.Syntax.GetLocation(), arguments));
    }
}
