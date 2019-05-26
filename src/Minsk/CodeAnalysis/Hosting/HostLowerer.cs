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
    }
}