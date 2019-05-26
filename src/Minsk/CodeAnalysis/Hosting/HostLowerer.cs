using System.Collections.Immutable;

using Minsk.CodeAnalysis.Binding;

namespace Minsk.CodeAnalysis.Hosting
{
    internal sealed class HostLowerer : BoundTreeRewriter
    {
        private HostLowerer()
        {
        }

        public static BoundBlockStatement Lower(BoundStatement statement)
        {
            var hostLowerer = new HostLowerer();
            var result = hostLowerer.RewriteStatement(statement);
            return Flatten(result);
        }

        protected override BoundStatement RewriteExpressionStatement(BoundExpressionStatement node)
        {
            var expression = (BoundExpressionStatement)base.RewriteExpressionStatement(node);

            var assignResult = new BoundAssignResultVariableStatement(expression.Expression.Type);

            return new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(expression, assignResult));
        }

        protected override BoundStatement RewriteVariableDeclaration(BoundVariableDeclaration node)
        {
            var variableDeclaration = (BoundVariableDeclaration)base.RewriteVariableDeclaration(node);

            var variableExpression = new BoundVariableExpression(variableDeclaration.Variable);
            var expressionStatement = new BoundExpressionStatement(variableExpression);

            var assignResult = new BoundAssignResultVariableStatement(variableDeclaration.Variable.Type);

            return new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                variableDeclaration,
                expressionStatement,
                assignResult));
        }
    }
}