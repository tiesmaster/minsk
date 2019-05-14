using System.Collections.Generic;
using System.Linq;
using Minsk.CodeAnalysis.Binding;
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
            foreach (var node in _root.GetDescendants().OfType<BoundVariableDeclaration>())
            {
                _ilBuilder.GetOrCreateVariableSlot(node.Variable);
            }
            foreach (var node in _root.GetDescendants().OfType<BoundVariableExpression>())
            {
                _ilBuilder.GetOrCreateVariableSlot(node.Variable);
            }
            foreach (var node in _root.GetDescendants().OfType<BoundAssignmentExpression>())
            {
                _ilBuilder.GetOrCreateVariableSlot(node.Variable);
            }

            EmitRestoreVariables();

            EmitBlockStatement(_root);

            // var x = 10
            // x
            // 
            // ---->
            // 
            // var x = 10;
            // ...
            // return x;
            // return variables(x);
            // return new object[] { $result, x};



            // var x = y;
            // 
            // ---->
            // 
            // f(var[1] args)
            // var x = y[0]
            EmitSaveVariables();

            // return $result;
            EmitPushResult();
            EmitEndOfMethod();
        }

        private void EmitPushResult()
        {
            _il.Emit(OpCodes.Ldloc_0);
        }

        private void EmitRestoreVariables()
        {
            // void InsertInstructionsAtBeginningOfMethod(ILProcessor il, IEnumerable<Instruction> instructions)
            // {
            //     foreach (var instruction in instructions.Reverse())
            //     {
            //         var firstInstruction = il.Body.Instructions.First();
            //         il.InsertBefore(firstInstruction, instruction);
            //     }
            // }

            // var instructionsToInsert = new List<Instruction>();
            foreach (var variableDef in _ilBuilder.Variables)
            {
                // load variables[i] from arguments, and unbox
                _il.Emit(OpCodes.Ldarg_0);
                _il.Emit(OpCodes.Ldc_I4, variableDef.VariableIndex);
                _il.Emit(OpCodes.Ldelem_Ref);
                _il.Emit(OpCodes.Unbox_Any, _ilBuilder.HostModule.ImportReference(variableDef.Variable.Type));

                // and store in given slot
                _il.Emit(OpCodes.Stloc, variableDef.Slot);

                // InsertInstructionsAtBeginningOfMethod(_il, instructionsToInsert);
                // instructionsToInsert.Clear();
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
                _il.Emit(OpCodes.Box, _ilBuilder.HostModule.ImportReference(variableDef.Variable.Type));

                _il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitEndOfMethod()
        {
            _il.Emit(OpCodes.Ret);
        }
    }
}