﻿using System;
using System.Collections.Generic;
using Minsk.CodeAnalysis.Binding;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal class Emitter
    {
        private readonly BoundStatement _root;
        private readonly ILProcessor _il;

        public Emitter(BoundStatement root, ILProcessor iLProcessor)
        {
            _root = root;
            _il = iLProcessor;
        }

        public void Emit()
        {
            EmitStatement(_root);
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
            throw new NotImplementedException();
            // var value = EmitExpression(node.Initializer);
            // _variables[node.Variable] = value;
            // _lastValue = value;
        }

        private void EmitBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
                EmitStatement(statement);
        }

        private void EmitExpressionStatement(BoundExpressionStatement node)
        {
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
                    _il.Append(_il.Create(OpCodes.Ldc_I4, (int)n.Value));
                    break;
                case bool b when b:
                    _il.Append(_il.Create(OpCodes.Ldc_I4_1));
                    break;
                case bool b when !b:
                    _il.Append(_il.Create(OpCodes.Ldc_I4_0));
                    break;
                default:
                    throw new Exception($"Unexpected literal value, type: {n.Value.GetType()}");
            }
        }

        private void EmitVariableExpression(BoundVariableExpression v)
        {
            throw new NotImplementedException();
            // return _variables[v.Variable];
        }

        private void EmitAssignmentExpression(BoundAssignmentExpression a)
        {
            throw new NotImplementedException();
            // var value = EvaluateExpression(a.Expression);
            // _variables[a.Variable] = value;
            // return value;
        }

        private void EmitUnaryExpression(BoundUnaryExpression u)
        {
            throw new NotImplementedException();
            // var operand = EmitExpression(u.Operand);

            // switch (u.Op.Kind)
            // {
            //     case BoundUnaryOperatorKind.Identity:
            //         return (int)operand;
            //     case BoundUnaryOperatorKind.Negation:
            //         return -(int)operand;
            //     case BoundUnaryOperatorKind.LogicalNegation:
            //         return !(bool)operand;
            //     default:
            //         throw new Exception($"Unexpected unary operator {u.Op}");
            // }
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
                case BoundBinaryOperatorKind.LogicalAnd:
                    _il.Append(_il.Create(OpCodes.And));
                    break;
                case BoundBinaryOperatorKind.LogicalOr:
                    _il.Append(_il.Create(OpCodes.Or));
                    break;
                case BoundBinaryOperatorKind.Equals:
                    _il.Append(_il.Create(OpCodes.Ceq));
                    break;
                case BoundBinaryOperatorKind.NotEquals:
                    _il.Append(_il.Create(OpCodes.Ceq));
                    _il.Append(_il.Create(OpCodes.Ldc_I4_0));
                    _il.Append(_il.Create(OpCodes.Ceq));
                    break;
                default:
                    throw new Exception($"Unexpected binary operator {b.Op}");
            }
        }
    }

    internal sealed class Evaluator
    {
        private readonly BoundStatement _root;
        private readonly Dictionary<VariableSymbol, object> _variables;

        private object _lastValue;

        public Evaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _root = root;
            _variables = variables;
        }

        public object Evaluate()
        {
            EvaluateStatement(_root);
            return _lastValue;
        }

        private void EvaluateStatement(BoundStatement node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.BlockStatement:
                    EvaluateBlockStatement((BoundBlockStatement)node);
                    break;
                case BoundNodeKind.VariableDeclaration:
                    EvaluateVariableDeclaration((BoundVariableDeclaration)node);
                    break;
                case BoundNodeKind.ExpressionStatement:
                    EvaluateExpressionStatement((BoundExpressionStatement)node);
                    break;
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private void EvaluateVariableDeclaration(BoundVariableDeclaration node)
        {
            var value = EvaluateExpression(node.Initializer);
            _variables[node.Variable] = value;
            _lastValue = value;
        }

        private void EvaluateBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
                EvaluateStatement(statement);
        }

        private void EvaluateExpressionStatement(BoundExpressionStatement node)
        {
            _lastValue = EvaluateExpression(node.Expression);
        }

        private object EvaluateExpression(BoundExpression node)
        {
            switch (node.Kind)
            {
                case BoundNodeKind.LiteralExpression:
                    return EvaluateLiteralExpression((BoundLiteralExpression)node);
                case BoundNodeKind.VariableExpression:
                    return EvaluateVariableExpression((BoundVariableExpression)node);
                case BoundNodeKind.AssignmentExpression:
                    return EvaluateAssignmentExpression((BoundAssignmentExpression)node);
                case BoundNodeKind.UnaryExpression:
                    return EvaluateUnaryExpression((BoundUnaryExpression)node);
                case BoundNodeKind.BinaryExpression:
                    return EvaluateBinaryExpression((BoundBinaryExpression)node);
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private static object EvaluateLiteralExpression(BoundLiteralExpression n)
        {
            return n.Value;
        }

        private object EvaluateVariableExpression(BoundVariableExpression v)
        {
            return _variables[v.Variable];
        }

        private object EvaluateAssignmentExpression(BoundAssignmentExpression a)
        {
            var value = EvaluateExpression(a.Expression);
            _variables[a.Variable] = value;
            return value;
        }

        private object EvaluateUnaryExpression(BoundUnaryExpression u)
        {
            var operand = EvaluateExpression(u.Operand);

            switch (u.Op.Kind)
            {
                case BoundUnaryOperatorKind.Identity:
                    return (int)operand;
                case BoundUnaryOperatorKind.Negation:
                    return -(int)operand;
                case BoundUnaryOperatorKind.LogicalNegation:
                    return !(bool)operand;
                default:
                    throw new Exception($"Unexpected unary operator {u.Op}");
            }
        }

        private object EvaluateBinaryExpression(BoundBinaryExpression b)
        {
            var left = EvaluateExpression(b.Left);
            var right = EvaluateExpression(b.Right);

            switch (b.Op.Kind)
            {
                case BoundBinaryOperatorKind.Addition:
                    return (int)left + (int)right;
                case BoundBinaryOperatorKind.Subtraction:
                    return (int)left - (int)right;
                case BoundBinaryOperatorKind.Multiplication:
                    return (int)left * (int)right;
                case BoundBinaryOperatorKind.Division:
                    return (int)left / (int)right;
                case BoundBinaryOperatorKind.LogicalAnd:
                    return (bool)left && (bool)right;
                case BoundBinaryOperatorKind.LogicalOr:
                    return (bool)left || (bool)right;
                case BoundBinaryOperatorKind.Equals:
                    return Equals(left, right);
                case BoundBinaryOperatorKind.NotEquals:
                    return !Equals(left, right);
                default:
                    throw new Exception($"Unexpected binary operator {b.Op}");
            }
        }
    }
}