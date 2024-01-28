using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Funx.Analyzer;

/// <summary>
/// A sample analyzer that reports invalid values being used for the 'speed' parameter of the 'SetSpeed' function.
/// To make sure that we analyze the method of the specific class, we use semantic analysis instead of the syntax tree, so this analyzer will not work if the project is not compilable.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class OptionSemanticAnalyzer : DiagnosticAnalyzer
{
    
    #region fields: 
    
    // Preferred format of DiagnosticId is Your Prefix + Number, e.g. CA1234.
    public const string DiagnosticId = "FX0001";
    // The category of the diagnostic (Design, Naming etc.).
    private const string Category = "Usage";

    // Feel free to use raw strings if you don't need localization.
    private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.FX0001Title),
        Resources.ResourceManager, typeof(Resources));

    // The message that will be displayed to the user.
    private static readonly LocalizableString MessageFormat =
        new LocalizableResourceString(nameof(Resources.FX0002MessageFormat), Resources.ResourceManager,
            typeof(Resources));

    private static readonly LocalizableString Description =
        new LocalizableResourceString(nameof(Resources.FX0002Description), Resources.ResourceManager,
            typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    // Keep in mind: you have to list your rules here.
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);
    
    #endregion

    public override void Initialize(AnalysisContext context)
    {
        // You must call this method to avoid analyzing generated code.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // You must call this method to enable the Concurrent Execution.
        context.EnableConcurrentExecution();

        // Subscribe to semantic (compile time) action invocation, e.g. throw statement
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Throw);
        
    }

    /// <summary>
    /// Executed on the completion of the semantic analysis associated with the Invocation operation.
    /// </summary>
    /// <param name="context">Operation context.</param>
    private void AnalyzeOperation(OperationAnalysisContext context)
    {
        // The Roslyn architecture is based on inheritance.
        // To get the required metadata, we should match the 'Operation' and 'Syntax' objects to the particular types,
        // which are based on the 'OperationKind' parameter specified in the 'Register...' method.
        if (context.Operation is not IThrowOperation throwOperation ||
            context.Operation.Syntax is not ThrowStatementSyntax throwExpressionSyntax)
            return;

        var semanticModel = context.Operation.SemanticModel;
        var syntaxNodeParent = GetContainingMethod(throwExpressionSyntax);

        if (semanticModel?.GetDeclaredSymbol(syntaxNodeParent) is IMethodSymbol methodSymbol)
        {
            var returnType = methodSymbol.ReturnType;
            
            if (IsOptionType(returnType, semanticModel))
            {
                var diagnostic = Diagnostic.Create(Rule,
                    // The highlighted area in the analyzed source code. Keep it as specific as possible.
                    context.Operation.Syntax.GetLocation(),
                    // The value is passed to the 'MessageFormat' argument of your rule.
                    string.Empty);
                
                // Reporting a diagnostic is the primary outcome of analyzers.
                context.ReportDiagnostic(diagnostic);
            }
        }

        return;

        static bool IsOptionType(ITypeSymbol typeSymbol, SemanticModel semanticModel)
        {
            // Assuming that Option<T> is defined in the same assembly, otherwise, you may need to adjust the condition
            return semanticModel.Compilation.GetTypeByMetadataName("Funx.Option`1") is { } optionTypeSymbol
                   && SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, optionTypeSymbol);
        }
        
        MethodDeclarationSyntax GetContainingMethod(SyntaxNode parent)
        {
            while (true)
            {
                if (parent is MethodDeclarationSyntax mds) return mds;
                parent = parent.Parent!;
            }
        }
    }
}
