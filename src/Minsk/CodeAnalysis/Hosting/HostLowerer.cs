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
            var boundExpressionStatement = (BoundExpressionStatement)base.RewriteExpressionStatement(node);

            var assignResult = new BoundAssignResultVariableStatement(boundExpressionStatement.Expression.Type);

            return new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(boundExpressionStatement, assignResult));
        }
    }
}