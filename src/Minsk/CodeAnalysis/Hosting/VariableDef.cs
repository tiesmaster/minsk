using Minsk.CodeAnalysis.Symbols;

namespace Minsk.CodeAnalysis.Hosting
{
    internal class VariableDef
    {
        public VariableDef(VariableSymbol variable, int slot)
        {
            Variable = variable;
            Slot = slot;
        }

        public VariableSymbol Variable { get; }
        public int Slot { get; }
        public int VariableIndex => Slot - 1;
    }
}