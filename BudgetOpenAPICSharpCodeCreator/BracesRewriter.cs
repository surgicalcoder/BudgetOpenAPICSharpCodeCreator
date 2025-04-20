using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BudgetOpenAPICSharpCodeCreator;

/// <summary>
/// This rewriter adds braces to if statements, for loops, foreach loops, and while loops 
/// that don't have braces.
/// </summary>
public class BracesRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
    {
        // First, visit nested statements to handle them
        node = (IfStatementSyntax)base.VisitIfStatement(node);
        
        // Check if this if statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        // Check and handle the else clause if it exists
        if (node.Else != null)
        {
            var elseStatement = node.Else.Statement;
            if (!(elseStatement is BlockSyntax) && !(elseStatement is IfStatementSyntax))
            {
                // Create a block with the original statement
                var block = SyntaxFactory.Block(elseStatement);
                
                // Replace the else statement with the block
                node = node.WithElse(SyntaxFactory.ElseClause(block));
            }
        }
        
        return node;
    }
    
    public override SyntaxNode VisitForStatement(ForStatementSyntax node)
    {
        // Visit nested statements first
        node = (ForStatementSyntax)base.VisitForStatement(node);
        
        // Check if this for statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
    
    public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
    {
        // Visit nested statements first
        node = (ForEachStatementSyntax)base.VisitForEachStatement(node);
        
        // Check if this foreach statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
    
    public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
    {
        // Visit nested statements first
        node = (WhileStatementSyntax)base.VisitWhileStatement(node);
        
        // Check if this while statement doesn't have braces
        if (!(node.Statement is BlockSyntax))
        {
            // Create a block with the original statement
            var block = SyntaxFactory.Block(node.Statement);
            
            // Replace the statement with the block
            node = node.WithStatement(block);
        }
        
        return node;
    }
}