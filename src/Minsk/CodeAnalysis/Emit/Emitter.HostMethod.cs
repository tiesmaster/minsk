using System;
using System.Collections.Generic;
using System.Linq;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Hosting;

using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis.Emit
{
    internal sealed partial class Emitter
    {
        public HostMethod EmitHostMethod(BoundProgram program)
        {
            EmitProgram(program);

            InsertEmitRestoreVariablesFromArgumentToStartOfMethod();
            EmitSaveVariables();

            EmitPushResult();
            EmitEndOfMethod();

            return _emitHelper.Finalize();
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
            foreach (var variableDef in _emitHelper.Variables)
            {
                // load variables[i] from arguments, and unbox
                instructionsToInsert.Add(_il.Create(OpCodes.Ldarg_0));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldc_I4, variableDef.VariableIndex));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldelem_Ref));
                instructionsToInsert.Add(_il.Create(OpCodes.Unbox_Any, _emitHelper.ImportReference(variableDef.Variable.Type)));

                // and store in given slot
                instructionsToInsert.Add(_il.Create(OpCodes.Stloc, variableDef.Slot));

                InsertInstructionsAtBeginningOfMethod(_il, instructionsToInsert);
                instructionsToInsert.Clear();
            }
        }

        private void EmitSaveVariables()
        {
            foreach (var variableDef in _emitHelper.Variables)
            {
                // load the in|out variable array
                _il.Emit(OpCodes.Ldarg_0);
                // index into the variable array
                _il.Emit(OpCodes.Ldc_I4, variableDef.VariableIndex);

                // load the variable from the given slot, and box it
                _il.Emit(OpCodes.Ldloc, variableDef.Slot);
                _il.Emit(OpCodes.Box, _emitHelper.ImportReference(variableDef.Variable.Type));

                _il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitEndOfMethod()
        {
            _il.Emit(OpCodes.Ret);
        }
    }
}