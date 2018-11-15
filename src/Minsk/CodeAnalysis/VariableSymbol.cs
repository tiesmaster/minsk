using System;

namespace Minsk.CodeAnalysis
{
    public sealed class VariableSymbol
    {
        public VariableSymbol(string name, bool isReadOnly, Type type)
        {
            Name = name;
            IsReadOnly = isReadOnly;
            Type = type;
        }

        public string Name { get; }
        public bool IsReadOnly { get; }
        public Type Type { get; }

        public override bool Equals(object obj)
        {
            if (obj is VariableSymbol variable)
            {
                return Equals(variable);
            }
            else
            {
                return false;
            }
        }

        public bool Equals(VariableSymbol other)
        {
            return Name == other.Name && Type == other.Type;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + Name.GetHashCode();
            hash = hash * 31 + Type.GetHashCode();
            return hash;
        }
    }
}