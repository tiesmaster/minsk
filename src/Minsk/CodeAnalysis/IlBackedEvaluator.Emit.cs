using System;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Symbols;

using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal sealed partial class IlBackedEvaluator
    {
        private void EmitBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
            {
                EmitStatement(statement);
            }
        }

        private void EmitStatement(BoundStatement statement)
        {
            switch (statement.Kind)
            {
                case BoundNodeKind.VariableDeclaration:
                    EmitVariableDeclaration((BoundVariableDeclaration)statement);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    EmitExpressionStatement((BoundExpressionStatement)statement);
                    break;
                case BoundNodeKind.GotoStatement:
                    EmitGotoStatement((BoundGotoStatement)statement);
                    break;
                case BoundNodeKind.ConditionalGotoStatement:
                    EmitConditionalGotoStatement((BoundConditionalGotoStatement)statement);
                    break;
                case BoundNodeKind.LabelStatement:
                    EmitLabelStatement((BoundLabelStatement)statement);
                    break;
                default:
                    throw new Exception($"Unexpected node {statement.Kind}");
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

        private void EmitExpressionStatement(BoundExpressionStatement node)
        {
            EmitExpression(node.Expression);
            EmitSaveResult(node.Expression.Type);
        }

        private void EmitGotoStatement(BoundGotoStatement node)
        {
            _ilBuilder.AddJump(OpCodes.Br_S, node.Label);
        }

        private void EmitConditionalGotoStatement(BoundConditionalGotoStatement node)
        {
            EmitExpression(node.Condition);
            var branchOpcode = node.JumpIfTrue ? OpCodes.Brtrue_S : OpCodes.Brfalse_S;
            _ilBuilder.AddJump(branchOpcode, node.Label);
        }

        private void EmitLabelStatement(BoundLabelStatement node)
        {
            _ilBuilder.MarkLabel(node.Label);
        }

        private void EmitSaveResult(TypeSymbol resultType)
        {
            _il.Emit(OpCodes.Box, _ilBuilder.ImportReference(resultType));
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
                case string s:
                    _il.Emit(OpCodes.Ldstr, s);
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
                case BoundUnaryOperatorKind.OnesComplement:
                    _il.Emit(OpCodes.Not);
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
                case BoundBinaryOperatorKind.BitwiseXor:
                    _il.Emit(OpCodes.Xor);
                    break;
                case BoundBinaryOperatorKind.LogicalAnd:
                case BoundBinaryOperatorKind.BitwiseAnd:
                    _il.Emit(OpCodes.And);
                    break;
                case BoundBinaryOperatorKind.LogicalOr:
                case BoundBinaryOperatorKind.BitwiseOr:
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
                case BoundBinaryOperatorKind.Less:
                    _il.Emit(OpCodes.Clt);
                    break;
                case BoundBinaryOperatorKind.LessOrEquals:
                    _il.Emit(OpCodes.Cgt);
                    _il.Emit(OpCodes.Ldc_I4_0);
                    _il.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.Greater:
                    _il.Emit(OpCodes.Cgt);
                    break;
                case BoundBinaryOperatorKind.GreaterOrEquals:
                    _il.Emit(OpCodes.Clt);
                    _il.Emit(OpCodes.Ldc_I4_0);
                    _il.Emit(OpCodes.Ceq);
                    break;
                default:
                    throw new Exception($"Unexpected binary operator {b.Op}: {b.Op.Kind}");
            }
        }
    }
}