using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Usemam.NLog.CodeFixes
{
    internal abstract class FixAllInDocumentProvider : FixAllProvider
    {
        protected abstract string ActionTitle { get; }

        /// <summary>
        /// Gets fix all occurrences fix for the given context.
        /// </summary>
        public override Task<CodeAction> GetFixAsync(FixAllContext context)
        {
            CodeAction fixAction = null;
            switch (context.Scope)
            {
                case FixAllScope.Document:
                    fixAction = CodeAction.Create(
                        this.ActionTitle,
                        cancellationToken => this.GetDocumentFixesAsync(context.WithCancellationToken(cancellationToken)),
                        nameof(FixAllInDocumentProvider));
                    break;

                case FixAllScope.Project:
                    fixAction = CodeAction.Create(
                        this.ActionTitle,
                        cancellationToken => this.GetProjectFixesAsync(context.WithCancellationToken(cancellationToken), context.Project),
                        nameof(FixAllInDocumentProvider));
                    break;

                case FixAllScope.Solution:
                    fixAction = CodeAction.Create(
                        this.ActionTitle,
                        cancellationToken => this.GetSolutionFixesAsync(context.WithCancellationToken(cancellationToken)),
                        nameof(FixAllInDocumentProvider));
                    break;
            }

            return Task.FromResult(fixAction);
        }

        /// <summary>
        /// Fixes all occurrences of a diagnostic in a specific document.
        /// </summary>
        /// <param name="context">The context for the Fix All operation.</param>
        /// <param name="document">The document to fix.</param>
        /// <param name="diagnostics">The diagnostics to fix in the document.</param>
        protected abstract Task<SyntaxNode> FixAllInDocumentAsync(
            FixAllContext context, Document document, ImmutableArray<Diagnostic> diagnostics);

        private async Task<Document> GetDocumentFixesAsync(FixAllContext context)
        {
            var documentDiagnosticsToFix = await GetDocumentDiagnosticsToFixAsync(context).ConfigureAwait(false);
            if (!documentDiagnosticsToFix.TryGetValue(context.Document, out var diagnostics))
            {
                return context.Document;
            }

            var newRoot = await FixAllInDocumentAsync(context, context.Document, diagnostics).ConfigureAwait(false);
            if (newRoot == null)
            {
                return context.Document;
            }

            return context.Document.WithSyntaxRoot(newRoot);
        }

        private async Task<Solution> GetSolutionFixesAsync(FixAllContext context, ImmutableArray<Document> documents)
        {
            var documentDiagnosticsToFix =
                await GetDocumentDiagnosticsToFixAsync(context).ConfigureAwait(false);

            Solution solution = context.Solution;
            List<Task<SyntaxNode>> newDocuments = new List<Task<SyntaxNode>>(documents.Length);
            foreach (var document in documents)
            {
                if (!documentDiagnosticsToFix.TryGetValue(document, out var diagnostics))
                {
                    newDocuments.Add(document.GetSyntaxRootAsync(context.CancellationToken));
                    continue;
                }

                newDocuments.Add(this.FixAllInDocumentAsync(context, document, diagnostics));
            }

            for (int i = 0; i < documents.Length; i++)
            {
                var newDocumentRoot = await newDocuments[i].ConfigureAwait(false);
                if (newDocumentRoot == null)
                {
                    continue;
                }

                solution = solution.WithDocumentSyntaxRoot(documents[i].Id, newDocumentRoot);
            }

            return solution;
        }

        private Task<Solution> GetProjectFixesAsync(FixAllContext context, Project project)
        {
            return GetSolutionFixesAsync(context, project.Documents.ToImmutableArray());
        }

        private Task<Solution> GetSolutionFixesAsync(FixAllContext context)
        {
            ImmutableArray<Document> documents =
                context.Solution.Projects.SelectMany(x => x.Documents).ToImmutableArray();
            return this.GetSolutionFixesAsync(context, documents);
        }

        private async Task<ImmutableDictionary<Document, ImmutableArray<Diagnostic>>> GetDocumentDiagnosticsToFixAsync(FixAllContext context)
        {
            var allDiagnostics = ImmutableArray<Diagnostic>.Empty;
            var projectsToFix = ImmutableArray<Project>.Empty;

            var document = context.Document;
            var project = context.Project;

            switch (context.Scope)
            {
                case FixAllScope.Document:
                    if (document != null)
                    {
                        var documentDiagnostics = await context.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                        return
                            ImmutableDictionary<Document, ImmutableArray<Diagnostic>>
                                .Empty
                                .SetItem(document, documentDiagnostics);
                    }

                    break;

                case FixAllScope.Project:
                    projectsToFix = ImmutableArray.Create(project);
                    allDiagnostics = await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                    break;

                case FixAllScope.Solution:
                    projectsToFix = project.Solution.Projects
                        .Where(p => p.Language == project.Language)
                        .ToImmutableArray();

                    var diagnostics = new ConcurrentDictionary<ProjectId, ImmutableArray<Diagnostic>>();
                    var tasks = new Task[projectsToFix.Length];
                    for (int i = 0; i < projectsToFix.Length; i++)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        var projectToFix = projectsToFix[i];
                        tasks[i] = Task.Run(
                            async () =>
                            {
                                var projectDiagnostics = await context.GetAllDiagnosticsAsync(projectToFix).ConfigureAwait(false);
                                diagnostics.TryAdd(projectToFix.Id, projectDiagnostics);
                            },
                            context.CancellationToken);
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    allDiagnostics = allDiagnostics.AddRange(
                        diagnostics.SelectMany(i => i.Value.Where(x => context.DiagnosticIds.Contains(x.Id))));
                    break;
            }

            if (allDiagnostics.IsEmpty)
            {
                return ImmutableDictionary<Document, ImmutableArray<Diagnostic>>.Empty;
            }

            return await GetDocumentDiagnosticsToFixAsync(allDiagnostics, projectsToFix, context.CancellationToken).ConfigureAwait(false);
        }

        private static async Task<ImmutableDictionary<Document, ImmutableArray<Diagnostic>>> GetDocumentDiagnosticsToFixAsync(
            ImmutableArray<Diagnostic> diagnostics,
            ImmutableArray<Project> projects,
            CancellationToken cancellationToken)
        {
            var treeToDocumentMap = await GetTreeToDocumentMapAsync(projects, cancellationToken).ConfigureAwait(false);

            var builder = ImmutableDictionary.CreateBuilder<Document, ImmutableArray<Diagnostic>>();
            foreach (var documentAndDiagnostics in diagnostics.GroupBy(d => GetReportedDocument(d, treeToDocumentMap)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = documentAndDiagnostics.Key;
                var diagnosticsForDocument = documentAndDiagnostics.ToImmutableArray();
                builder.Add(document, diagnosticsForDocument);
            }

            return builder.ToImmutable();
        }

        private static async Task<ImmutableDictionary<SyntaxTree, Document>> GetTreeToDocumentMapAsync(ImmutableArray<Project> projects, CancellationToken cancellationToken)
        {
            var builder = ImmutableDictionary.CreateBuilder<SyntaxTree, Document>();
            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var document in project.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    builder.Add(tree, document);
                }
            }

            return builder.ToImmutable();
        }

        private static Document GetReportedDocument(Diagnostic diagnostic, ImmutableDictionary<SyntaxTree, Document> treeToDocumentsMap)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree == null)
            {
                return null;
            }

            return treeToDocumentsMap.TryGetValue(tree, out var document) ? document : null;
        }
    }
}