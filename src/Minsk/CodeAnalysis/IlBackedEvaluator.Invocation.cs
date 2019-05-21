using System;
using System.Linq;

namespace Minsk.CodeAnalysis
{
    internal sealed partial class IlBackedEvaluator
    {
        private object InvokeHostMethod(HostMethod hostMethod)
        {
            var variableValues = CreateVariablesParameter();
            var result = hostMethod.Invoke(variableValues);
            CopyVariablesBackToDictionary(variableValues);

            return result;
        }

        private object[] CreateVariablesParameter()
        {
            return (from variableDef in _ilBuilder.Variables
                    orderby variableDef.Slot
                    let variable = variableDef.Variable
                    select _variables.TryGetValue(variable, out var value)
                        ? value
                        : Activator.CreateInstance(_ilBuilder.ToClrType(variable.Type))
            ).ToArray();
        }

        private void CopyVariablesBackToDictionary(object[] variableValues)
        {
            foreach (var variableDef in _ilBuilder.Variables)
            {
                _variables[variableDef.Variable] = variableValues[variableDef.VariableIndex];
            }
        }
    }
}