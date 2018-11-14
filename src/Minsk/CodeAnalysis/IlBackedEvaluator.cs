using System;
using System.Collections.Generic;
using System.Reflection;
using Minsk.CodeAnalysis.Binding;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal sealed class IlBackedEvaluator
    {
        private readonly BoundStatement _root;
        private readonly Dictionary<VariableSymbol, int> _variables;
        private int _nextFreeVariableSlot;

        // private object _lastValue;
        private ILProcessor _il;

        public IlBackedEvaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _root = root;
            _variables = new Dictionary<VariableSymbol, int>();
        }

        public object Evaluate()
        {
            var ilBuilder = new HostMethodBuilder();
            _il = ilBuilder.HostMethodIlProcessor;

            EmitStatement(_root);
            _il.Append(_il.Create(OpCodes.Ret));

            var hostMethod = ilBuilder.Build();
            var result = hostMethod.Invoke();

            return result;
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
            if (_variables.TryGetValue(node.Variable, out var slot))
            {
                _il.Append(_il.Create(OpCodes.Stloc, slot));
            }
            else
            {
                slot = _nextFreeVariableSlot++;
                _variables[node.Variable] = slot;
                _il.Append(_il.Create(OpCodes.Stloc, slot));
            }
            // _lastValue = value;
        }

        private void EmitBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
                EmitStatement(statement);
        }

        private void EmitExpressionStatement(BoundExpressionStatement node)
        {
            // _lastValue = EvaluateExpression(node.Expression);
            EmitExpression(node.Expression);
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
                // case BoundNodeKind.AssignmentExpression:
                //     return EvaluateAssignmentExpression((BoundAssignmentExpression)node);
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
            _il.Append(_il.Create(OpCodes.Ldc_I4, (int)n.Value));
        }

        private void EmitVariableExpression(BoundVariableExpression v)
        {
            var slot = _variables[v.Variable];
            _il.Append(_il.Create(OpCodes.Ldloc, slot));
        }

        // private object EvaluateAssignmentExpression(BoundAssignmentExpression a)
        // {
        //     var value = EvaluateExpression(a.Expression);
        //     _variables[a.Variable] = value;
        //     return value;
        // }

        private void EmitUnaryExpression(BoundUnaryExpression u)
        {
            EmitExpression(u.Operand);

            switch (u.Op.Kind)
            {
                case BoundUnaryOperatorKind.Identity:
                    break;
                case BoundUnaryOperatorKind.Negation:
                    _il.Append(_il.Create(OpCodes.Neg));
                    break;
                // case BoundUnaryOperatorKind.LogicalNegation:
                //     return !(bool)operand;
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
                    _il.Append(_il.Create(OpCodes.Add));
                    break;
                case BoundBinaryOperatorKind.Subtraction:
                    _il.Append(_il.Create(OpCodes.Sub));
                    break;
                case BoundBinaryOperatorKind.Multiplication:
                    _il.Append(_il.Create(OpCodes.Mul));
                    break;
                case BoundBinaryOperatorKind.Division:
                    _il.Append(_il.Create(OpCodes.Div));
                    break;
                // case BoundBinaryOperatorKind.LogicalAnd:
                //     return (bool)left && (bool)right;
                // case BoundBinaryOperatorKind.LogicalOr:
                //     return (bool)left || (bool)right;
                // case BoundBinaryOperatorKind.Equals:
                //     return Equals(left, right);
                // case BoundBinaryOperatorKind.NotEquals:
                //     return !Equals(left, right);
                default:
                    throw new Exception($"Unexpected binary operator {b.Op}");
            }
        }
    }
}