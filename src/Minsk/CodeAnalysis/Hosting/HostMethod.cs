using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Minsk.CodeAnalysis.Symbols;

namespace Minsk.CodeAnalysis.Hosting
{
    internal class HostMethod
    {
        private readonly HostMethodDefinition _hostMethodDefinition;
        private readonly byte[] _peBytes;
        private readonly IEnumerable<VariableDef> _variableDefinitions;

        public HostMethod(
            HostMethodDefinition hostMethodDefinition,
            byte[] peBytes,
            IEnumerable<VariableDef> variableDefinitions)
        {
            _hostMethodDefinition = hostMethodDefinition;
            _peBytes = peBytes;
            _variableDefinitions = variableDefinitions;
        }

        public object Run(Dictionary<VariableSymbol, object> variables)
        {
            var variableValues = CreateVariablesParameter(variables);
            var result = Invoke(variableValues);
            CopyVariablesBackToDictionary(variableValues, variables);

            return result;
        }

        private object[] CreateVariablesParameter(Dictionary<VariableSymbol, object> variables)
        {
            return (from variableDef in _variableDefinitions
                    orderby variableDef.Slot
                    let variable = variableDef.Variable
                    select variables.TryGetValue(variable, out var value)
                        ? value
                        : Activator.CreateInstance(variable.Type.ClrType)
            ).ToArray();
        }

        private object Invoke(object[] variables)
        {
            var hostType = GetHostAssembly().GetType(_hostMethodDefinition.TypeName);

            var hostMethod = hostType.GetMethod(
                _hostMethodDefinition.MethodName,
                BindingFlags.Static | BindingFlags.Public);

            var result = hostMethod.Invoke(null, new object[] { variables });

            return result;
        }

        private Assembly GetHostAssembly() => Assembly.Load(_peBytes);

        private void CopyVariablesBackToDictionary(object[] variableValuesInHost, Dictionary<VariableSymbol, object> variables)
        {
            foreach (var variableDef in _variableDefinitions)
            {
                variables[variableDef.Variable] = variableValuesInHost[variableDef.VariableIndex];
            }
        }
    }
}