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
            Il.Emit(OpCodes.Ldloc_0);
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
            foreach (var variableDef in CurrentMethodFrame.Variables)
            {
                // load variables[i] from arguments, and unbox
                instructionsToInsert.Add(Il.Create(OpCodes.Ldarg_0));
                instructionsToInsert.Add(Il.Create(OpCodes.Ldc_I4, variableDef.VariableIndex));
                instructionsToInsert.Add(Il.Create(OpCodes.Ldelem_Ref));
                instructionsToInsert.Add(Il.Create(OpCodes.Unbox_Any, _emitHelper.ImportReference(variableDef.Variable.Type)));

                // and store in given slot
                instructionsToInsert.Add(Il.Create(OpCodes.Stloc, variableDef.Slot));

                InsertInstructionsAtBeginningOfMethod(Il, instructionsToInsert);
                instructionsToInsert.Clear();
            }
        }

        private void EmitSaveVariables()
        {
            foreach (var variableDef in CurrentMethodFrame.Variables)
            {
                // load the in|out variable array
                Il.Emit(OpCodes.Ldarg_0);
                // index into the variable array
                Il.Emit(OpCodes.Ldc_I4, variableDef.VariableIndex);

                // load the variable from the given slot, and box it
                Il.Emit(OpCodes.Ldloc, variableDef.Slot);
                Il.Emit(OpCodes.Box, _emitHelper.ImportReference(variableDef.Variable.Type));

                Il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitEndOfMethod()
        {
            Il.Emit(OpCodes.Ret);
        }
    }
}