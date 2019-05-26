using Minsk.CodeAnalysis.Symbols;

namespace Minsk.CodeAnalysis.Binding
{
    internal class BoundAssignResultVariableStatement : BoundStatement
    {
        public BoundAssignResultVariableStatement(TypeSymbol type)
        {
            Type = type;
        }

        public override BoundNodeKind Kind => BoundNodeKind.AssignResultVariableStatement;
        public TypeSymbol Type { get; }
    }
}