﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public class PublicToInternalGenerator : IIncrementalGenerator
{
  public void Initialize(IncrementalGeneratorInitializationContext context)
  {
    // System.Diagnostics.Debugger.Launch();

    // Create a provider for additional files that end with "Annotations.cs"
    var additionalFilesProvider = context.AdditionalTextsProvider
      .Where(file => file.Path.EndsWith("Annotations.cs", StringComparison.OrdinalIgnoreCase));

    // Transform each additional file into a generated source
    var generatedSources = additionalFilesProvider
      .Select(ProcessFile)
      .Where(result => result != null); // Filter out null results

    // Register the source output
    context.RegisterSourceOutput(generatedSources, (sourceProductionContext, source) =>
    {
      if (source != null)
      {
        sourceProductionContext.AddSource(source.Value.fileName, source.Value.sourceText);
      }
    });
  }

  private static (string fileName, string sourceText)?
    ProcessFile(AdditionalText file, CancellationToken cancellationToken)
  {
    var sourceText = file.GetText(cancellationToken);
    if (sourceText == null) return null;

    var syntaxTree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
    var root = syntaxTree.GetCompilationUnitRoot(cancellationToken);

    var rewriter = new PublicToInternalRewriter();

    var newRoot = rewriter.Visit(root);
    if (newRoot == root) return null; // only generate if changes were made

    var newSourceText = newRoot.GetText(Encoding.UTF8).ToString();
    var fileName = Path.GetFileNameWithoutExtension(file.Path);
    var newFileName = $"{fileName}.Internal.cs";

    return (newFileName, newSourceText);
  }
}

internal class PublicToInternalRewriter : CSharpSyntaxRewriter
{
  public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
  {
    var newNode = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
  {
    var newNode = (InterfaceDeclarationSyntax?)base.VisitInterfaceDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
  {
    var newNode = (StructDeclarationSyntax?)base.VisitStructDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
  {
    var newNode = (EnumDeclarationSyntax?)base.VisitEnumDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node)
  {
    var newNode = (DelegateDeclarationSyntax?)base.VisitDelegateDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
  {
    var newNode = (RecordDeclarationSyntax?)base.VisitRecordDeclaration(node);
    return ChangePublicToInternal(newNode);
  }

  private static TMemberDeclarationSyntax? ChangePublicToInternal<TMemberDeclarationSyntax>(TMemberDeclarationSyntax? node)
    where TMemberDeclarationSyntax : MemberDeclarationSyntax
  {
    if (node is null) return null;

    // check if this is a top-level type (not nested)
    if (IsNestedType(node)) return node;

    var modifiers = node.Modifiers;

    // find public modifier
    var publicModifier = modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PublicKeyword));
    if (publicModifier.IsKind(SyntaxKind.None))
      return node;

    // Replace public with internal
    var internalToken = SyntaxFactory.Token(SyntaxKind.InternalKeyword)
      .WithLeadingTrivia(publicModifier.LeadingTrivia)
      .WithTrailingTrivia(publicModifier.TrailingTrivia);

    var newModifiersList = modifiers.Replace(publicModifier, internalToken);
    return (TMemberDeclarationSyntax)node.WithModifiers(newModifiersList);
  }

  private static bool IsNestedType(MemberDeclarationSyntax node)
  {
    var parent = node.Parent;
    while (parent != null)
    {
      if (parent is ClassDeclarationSyntax or StructDeclarationSyntax or InterfaceDeclarationSyntax or RecordDeclarationSyntax)
        return true;

      if (parent is NamespaceDeclarationSyntax or FileScopedNamespaceDeclarationSyntax or CompilationUnitSyntax)
        return false;

      parent = parent.Parent;
    }

    return false;
  }
}