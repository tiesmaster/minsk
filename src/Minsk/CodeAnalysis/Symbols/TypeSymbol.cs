using System;

namespace Minsk.CodeAnalysis.Symbols
{
    public sealed class TypeSymbol  : Symbol
    {
        public static readonly TypeSymbol Error = new TypeSymbol("?");
        public static readonly TypeSymbol Bool = new TypeSymbol("bool");
        public static readonly TypeSymbol Int = new TypeSymbol("int");
        public static readonly TypeSymbol String = new TypeSymbol("string");
        public static readonly TypeSymbol Void = new TypeSymbol("void");

        private TypeSymbol(string name)
            : base(name)
        {
        }

        public override SymbolKind Kind => SymbolKind.Type;

        public Type ClrType
        {
            get
            {
                if (this == Bool)
                {
                    return typeof(bool);
                }

                if (this == Int)
                {
                    return typeof(int);
                }

                if (this == String)
                {
                    return typeof(string);
                }

                throw new Exception($"Unsupported TypeSymbol given: {this}");
            }
        }
    }
}