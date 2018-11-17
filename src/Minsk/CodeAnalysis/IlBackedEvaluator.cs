using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Minsk.CodeAnalysis.Binding;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal sealed class IlBackedEvaluator
    {
        private readonly BoundStatement _root;
        private readonly Dictionary<VariableSymbol, object> _variables;

        private HostMethodBuilder _ilBuilder;
        private ILProcessor _il;

        public IlBackedEvaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables)
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
            EmitStatement(_root);

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
            // void InsertInstructions
            var instructionsToInsert = new List<Instruction>();
            foreach (var variableDef in _ilBuilder.Variables)
            {
                // load variables[i] from arguments, and unbox
                instructionsToInsert.Add(_il.Create(OpCodes.Ldarg_0));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldc_I4, variableDef.VariableIndex));
                instructionsToInsert.Add(_il.Create(OpCodes.Ldelem_Ref));
                instructionsToInsert.Add(_il.Create(OpCodes.Unbox_Any, _ilBuilder.HostModule.ImportReference(variableDef.Variable.Type)));

                // and store in given slot
                instructionsToInsert.Add(_il.Create(OpCodes.Stloc, variableDef.Slot));

                // add all instructions to insert at position 0
                instructionsToInsert.Reverse();
                foreach (var instruction in instructionsToInsert)
                {
                    var firstInstruction = _il.Body.Instructions.First();
                    _il.InsertBefore(firstInstruction, instruction);

                }
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
                _il.Emit(OpCodes.Box, _ilBuilder.HostModule.ImportReference(variableDef.Variable.Type));

                _il.Emit(OpCodes.Stelem_Ref);
            }
        }

        private void EmitEndOfMethod()
        {
            _il.Emit(OpCodes.Ret);
        }

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
                        : Activator.CreateInstance(variable.Type)
            ).ToArray();
        }

        private void CopyVariablesBackToDictionary(object[] variableValues)
        {
            foreach (var variableDef in _ilBuilder.Variables)
            {
                _variables[variableDef.Variable] = variableValues[variableDef.VariableIndex];
            }
        }

        private void EmitStatement(BoundStatement node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.BlockStatement:
                    EmitBlockStatement((BoundBlockStatement)node);
                    break;
                case BoundNodeKind.VariableDeclaration:
                    EmitVariableDeclaration((BoundVariableDeclaration)node);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    EmitExpressionStatement((BoundExpressionStatement)node);
                    break;
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private void EmitVariableDeclaration(BoundVariableDeclaration node)
        {
            EmitExpression(node.Initializer);
            var slot = _ilBuilder.GetOrCreateVariableSlot(node.Variable);
            _il.Emit(OpCodes.Stloc, slot);

            _il.Emit(OpCodes.Ldloc, slot);
            EmitSaveResult(node.Variable.Type);
        }

        private void EmitBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
                EmitStatement(statement);
        }

        private void EmitExpressionStatement(BoundExpressionStatement node)
        {
            EmitExpression(node.Expression);
            EmitSaveResult(node.Expression.Type);
        }

        private void EmitSaveResult(Type resultType)
        {
            _il.Emit(OpCodes.Box, _ilBuilder.HostModule.ImportReference(resultType));
            _il.Emit(OpCodes.Stloc_0);
        }

        private void EmitExpression(BoundExpression node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.LiteralExpression:
                    EmitLiteralExpression((BoundLiteralExpression)node);
                    break;
                case BoundNodeKind.VariableExpression:
                    EmitVariableExpression((BoundVariableExpression)node);
                    break;
                case BoundNodeKind.AssignmentExpression:
                    EmitAssignmentExpression((BoundAssignmentExpression)node);
                    break;
                case BoundNodeKind.UnaryExpression:
                    EmitUnaryExpression((BoundUnaryExpression)node);
                    break;
                case BoundNodeKind.BinaryExpression:
                    EmitBinaryExpression((BoundBinaryExpression)node);
                    break;
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private void EmitLiteralExpression(BoundLiteralExpression n)
        {
            switch (n.Value)
            {
                case int i:
                    _il.Emit(OpCodes.Ldc_I4, i);
                    break;
                case bool b when b:
                    _il.Emit(OpCodes.Ldc_I4_1);
                    break;
                case bool b when !b:
                    _il.Emit(OpCodes.Ldc_I4_0);
                    break;
                default:
                    throw new Exception($"Unexpected type '{n.Type}' for literal value");
            }
        }

        private void EmitVariableExpression(BoundVariableExpression v)
        {
            var slot = _ilBuilder.GetOrCreateVariableSlot(v.Variable);
            _il.Emit(OpCodes.Ldloc, slot);
        }

        private void EmitAssignmentExpression(BoundAssignmentExpression a)
        {
            EmitExpression(a.Expression);
            var slot = _ilBuilder.GetOrCreateVariableSlot(a.Variable);
            _il.Emit(OpCodes.Stloc, slot);

            // push result of the expression back on the stack, since this expression also produces a value downstream
            _il.Emit(OpCodes.Ldloc, slot);
        }

        private void EmitUnaryExpression(BoundUnaryExpression u)
        {
            EmitExpression(u.Operand);

            switch (u.Op.Kind)
            {
                case BoundUnaryOperatorKind.Identity:
                    break;
                case BoundUnaryOperatorKind.Negation:
                    _il.Emit(OpCodes.Neg);
                    break;
                case BoundUnaryOperatorKind.LogicalNegation:
                    _il.Emit(OpCodes.Ldc_I4_0);
                    _il.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new Exception($"Unexpected unary operator {u.Op}");
            }
        }

        private void EmitBinaryExpression(BoundBinaryExpression b)
        {
            EmitExpression(b.Left);
            EmitExpression(b.Right);

            switch (b.Op.Kind)
            {
                case BoundBinaryOperatorKind.Addition:
                    _il.Emit(OpCodes.Add);
                    break;
                case BoundBinaryOperatorKind.Subtraction:
                    _il.Emit(OpCodes.Sub);
                    break;
                case BoundBinaryOperatorKind.Multiplication:
                    _il.Emit(OpCodes.Mul);
                    break;
                case BoundBinaryOperatorKind.Division:
                    _il.Emit(OpCodes.Div);
                    break;
                case BoundBinaryOperatorKind.LogicalAnd:
                    _il.Emit(OpCodes.And);
                    break;
                case BoundBinaryOperatorKind.LogicalOr:
                    _il.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorKind.Equals:
                    _il.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.NotEquals:
                    _il.Emit(OpCodes.Ceq);
                    _il.Emit(OpCodes.Ldc_I4_0);
                    _il.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new Exception($"Unexpected binary operator {b.Op}");
            }
        }
    }
}