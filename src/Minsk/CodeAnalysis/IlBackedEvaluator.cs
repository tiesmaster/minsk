using System.Collections.Generic;
using System.Linq;
using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Symbols;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal sealed partial class IlBackedEvaluator
    {
        private readonly BoundBlockStatement _root;
        private readonly Dictionary<VariableSymbol, object> _variables;

        private HostMethodBuilder _ilBuilder;
        private ILProcessor _il;

        public IlBackedEvaluator(BoundBlockStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _root = root;
            _variables = variables;
        }

        public object Evaluate()
        {
            Initialize();
            EmitHostMethod();
            var result = InvokeHostMethod(_ilBuilder.Build());

            return result;
        }

        private void Initialize()
        {
            _ilBuilder = new HostMethodBuilder();
            _il = _ilBuilder.HostMethodIlProcessor;
        }

        private void EmitHostMethod()
        {
            EmitBlockStatement(_root);

            InsertEmitRestoreVariablesFromArgumentToStartOfMethod();
            EmitSaveVariables();

            EmitPushResult();
            EmitEndOfMethod();
        }

        private void EmitPushResult()
        {
            _il.Emit(OpCodes.Ldloc_0);
        }

        private void InsertEmitRestoreVariablesFromArgumentToStartOfMethod()
        {
            void InsertInstructionsAtBeginningOfMethod(ILProcessor il, IEnumerable<Instruction> instructions)
            {
                foreach (var instruction in instructions.Reverse())
                {
                    var firstInstruction = il.Body.Instructions.First();
                    il.InsertBefore(firstInstruction, instruction);
                }
            }

            var instructionsToInsert = new List<Instruction>();
            foreach (var variableDef in _ilBuilder.Variables)
            {
                // load variables[i] from arguments, and unbox
                instructionsToInsert.Add(_il.Create(OpCodes.Ldarg_0));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldc_I4, variableDef.VariableIndex));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldelem_Ref));
                instructionsToInsert.Add(_il.Create(OpCodes.Unbox_Any, _ilBuilder.ImportReference(variableDef.Variable.Type)));

                // and store in given slot
                instructionsToInsert.Add(_il.Create(OpCodes.Stloc, variableDef.Slot));

                InsertInstructionsAtBeginningOfMethod(_il, instructionsToInsert);
                instructionsToInsert.Clear();
            }
        }

        private void EmitSaveVariables()
        {
            foreach (var variableDef in _ilBuilder.Variables)
            {
                // load the in|out variable array
                _il.Emit(OpCodes.Ldarg_0);
                // index into the variable array
                _il.Emit(OpCodes.Ldc_I4, variableDef.VariableIndex);

                // load the variable from the given slot, and box it
                _il.Emit(OpCodes.Ldloc, variableDef.Slot);
                _il.Emit(OpCodes.Box, _ilBuilder.ImportReference(variableDef.Variable.Type));

                _il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitEndOfMethod()
        {
            _il.Emit(OpCodes.Ret);
        }
    }
}