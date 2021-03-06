﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lucene.Net.CodeAnalysis
{
    [ExportCodeFixProvider(LanguageNames.VisualBasic, Name = nameof(Lucene1000_SealIncrementTokenMethodVBCodeFixProvider)), Shared]
    public class Lucene1000_SealIncrementTokenMethodVBCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add NotOverridable keyword to IncrementToken() method";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(Lucene1000_TokenStreamOrItsIncrementTokenMethodMustBeSealedAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ClassBlockSyntax>().First();

            var incrementTokenMethodDeclaration = GetIncrementTokenMethodDeclaration(declaration);

            // If we can't find the method, we skip registration for this fix
            if (incrementTokenMethodDeclaration != null)
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: Title,
                        createChangedDocument: c => AddSealedKeywordAsync(context.Document, incrementTokenMethodDeclaration, c),
                        equivalenceKey: Title),
                    diagnostic);
            }
        }

        private async Task<Document> AddSealedKeywordAsync(Document document, MethodStatementSyntax methodDeclaration, CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(document);

            DeclarationModifiers modifiers = DeclarationModifiers.None;
            if (methodDeclaration.Modifiers.Any(SyntaxKind.NewKeyword))
            {
                modifiers |= DeclarationModifiers.New;
            }
            if (methodDeclaration.Modifiers.Any(SyntaxKind.OverridesKeyword))
            {
                modifiers |= DeclarationModifiers.Override;
            }
            modifiers |= DeclarationModifiers.Sealed;

            var newMethodDeclaration = generator.WithModifiers(methodDeclaration, modifiers);

            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(methodDeclaration, newMethodDeclaration);

            // Return document with transformed tree.
            return document.WithSyntaxRoot(newRoot);
        }

        private MethodStatementSyntax GetIncrementTokenMethodDeclaration(ClassBlockSyntax classBlock)
        {
            foreach (var member in classBlock.Members.Where(m => m.IsKind(SyntaxKind.FunctionBlock)))
            {
                var functionBlock = (MethodBlockSyntax)member;

                var methodDeclaration = (MethodStatementSyntax)functionBlock.BlockStatement;

                if (methodDeclaration.Identifier.ValueText == "IncrementToken")
                {
                    return methodDeclaration;
                }
            }
            return null;
        }
    }
}
