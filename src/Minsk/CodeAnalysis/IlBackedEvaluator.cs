using System.Collections.Generic;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Emit;
using Minsk.CodeAnalysis.Hosting;
using Minsk.CodeAnalysis.Symbols;

namespace Minsk.CodeAnalysis
{
    internal sealed class IlBackedEvaluator
    {
        private const string _hostAssemblyName = "HostAssembly";
        private const string _hostTypeName = "HostType";
        private const string _hostMethodName = "HostMethod";

        private readonly HostMethodDefinition _hostMethodDefinition =
            new HostMethodDefinition(_hostAssemblyName, _hostTypeName, _hostMethodName);

        private readonly BoundBlockStatement _root;
        private readonly Dictionary<VariableSymbol, object> _variables;

        public IlBackedEvaluator(BoundBlockStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _root = root;
            _variables = variables;
        }

        public object Evaluate()
        {
            var emitter = new Emitter(_hostMethodDefinition);

            var hostMethod = emitter.EmitHostMethod(_root);
            var result = hostMethod.Run(_variables);

            return result;
        }
    }
}