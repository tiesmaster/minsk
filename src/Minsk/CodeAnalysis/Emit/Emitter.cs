using System;
using System.Reflection;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Hosting;
using Minsk.CodeAnalysis.Symbols;

using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis.Emit
{
    internal sealed partial class Emitter
    {
        private readonly EmitHelper _emitHelper;
        private readonly ILProcessor _il;

        public Emitter(HostMethodDefinition hostMethodDefinition)
        {
            _emitHelper = new EmitHelper(hostMethodDefinition);
            _il = _emitHelper.HostMethodIlProcessor;
        }

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
                case BoundNodeKind.AssignResultVariableStatement:
                    EmitAssignResultVariableStatement((BoundAssignResultVariableStatement)statement);
                    break;
                default:
                    throw new Exception($"Unexpected node {statement.Kind}");
            }
        }

        private void EmitVariableDeclaration(BoundVariableDeclaration node)
        {
            EmitExpression(node.Initializer);
            var slot = _emitHelper.GetOrCreateVariableSlot(node.Variable);
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
            _emitHelper.AddJump(OpCodes.Br_S, node.Label);
        }

        private void EmitConditionalGotoStatement(BoundConditionalGotoStatement node)
        {
            EmitExpression(node.Condition);
            var branchOpcode = node.JumpIfTrue ? OpCodes.Brtrue_S : OpCodes.Brfalse_S;
            _emitHelper.AddJump(branchOpcode, node.Label);
        }

        private void EmitLabelStatement(BoundLabelStatement node)
        {
            _emitHelper.MarkLabel(node.Label);
        }

        private void EmitAssignResultVariableStatement(BoundAssignResultVariableStatement node)
        {
        }

        private void EmitSaveResult(TypeSymbol resultType)
        {
            if (resultType != TypeSymbol.Void)
            {
                _il.Emit(OpCodes.Box, _emitHelper.ImportReference(resultType));
            }

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
                case BoundNodeKind.CallExpression:
                    EmitCallExpression((BoundCallExpression)node);
                    break;
                case BoundNodeKind.ConversionExpression:
                    EmitConversionExpression((BoundConversionExpression)node);
                    break;
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        private void EmitLiteralExpression(BoundLiteralExpression n)
        {
            if (n.Type == TypeSymbol.Bool)
            {
                EmitBoolLiteral((bool)n.Value);
            }
            else if (n.Type == TypeSymbol.Int)
            {
                EmitIntLiteral((int)n.Value);
            }
            else if (n.Type == TypeSymbol.String)
            {
                EmitStringLiteral((string)n.Value);
            }
        }

        private void EmitBoolLiteral(bool value)
        {
            _il.Emit(OpCodes.Ldc_I4, value ? 1 : 0);
        }

        private void EmitIntLiteral(int value)
        {
            _il.Emit(OpCodes.Ldc_I4, value);
        }

        private void EmitStringLiteral(string value)
        {
            _il.Emit(OpCodes.Ldstr, value);
        }

        private void EmitVariableExpression(BoundVariableExpression v)
        {
            var slot = _emitHelper.GetOrCreateVariableSlot(v.Variable);
            _il.Emit(OpCodes.Ldloc, slot);
        }

        private void EmitAssignmentExpression(BoundAssignmentExpression a)
        {
            EmitExpression(a.Expression);
            var slot = _emitHelper.GetOrCreateVariableSlot(a.Variable);
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

        private void EmitCallExpression(BoundCallExpression node)
        {
            foreach (var argument in node.Arguments)
            {
                EmitExpression(argument);
            }

            var builtinFunctionWrapperMethod = BuiltinFunctionImplementations.LookupFunction(node);
            _il.Emit(OpCodes.Call, _emitHelper.ImportReference(builtinFunctionWrapperMethod));
        }

        private void EmitConversionExpression(BoundConversionExpression node)
        {
            EmitExpression(node.Expression);

            var conversionFunctionMethodWrapper = BuiltinFunctionImplementations.LookupFunction(node);
            _il.Emit(OpCodes.Call, _emitHelper.ImportReference(conversionFunctionMethodWrapper));
        }
    }

    public static class BuiltinFunctionImplementations
    {
        internal static MethodInfo LookupFunction(BoundConversionExpression node)
        {
            string name = $"{ToCamelCase(node.Expression.Type)}To{ToCamelCase(node.Type)}";
            return typeof(BuiltinFunctionImplementations).GetMethod(name);
        }

        internal static MethodInfo LookupFunction(BoundCallExpression node)
        {
            return typeof(BuiltinFunctionImplementations).GetMethod(ToCamelCase(node.Function.Name));
        }

        private static string ToCamelCase(TypeSymbol type) => ToCamelCase(type.ToString());

        private static string ToCamelCase(string name)
        {
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        public static bool StringToBool(string value) => Convert.ToBoolean(value);
        public static int StringToInt(string value) => Convert.ToInt32(value);
        public static string BoolToString(bool value) => Convert.ToString(value);
        public static string IntToString(int value) => Convert.ToString(value);

        public static string Input() => Console.ReadLine();

        public static object Print(string value)
        {
            Console.WriteLine(value);
            return null;
        }

        public static int Rnd(int maxValue)
        {
            var random = new Random();
            return random.Next(maxValue);
        }
    }
}