using System;
using System.Collections.Immutable;
using System.Reflection;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Hosting;
using Minsk.CodeAnalysis.Symbols;

using Mono.Cecil.Cil;
using Mono.Cecil;

namespace Minsk.CodeAnalysis.Emit
{
    internal sealed partial class Emitter
    {
        private readonly EmitHelper _emitHelper;

        public Emitter(HostMethodDefinition hostMethodDefinition)
        {
            _emitHelper = new EmitHelper(hostMethodDefinition);
        }

        public EmittingMethodFrame CurrentMethodFrame => _emitHelper.CurrentMethodFrame;
        public ILProcessor Il => CurrentMethodFrame.IlProcessor;

        private void EmitProgram(BoundProgram program)
        {
            EmitBlockStatement(program.Statement);
            EmitFunctions(program.Functions);
        }

        private void EmitBlockStatement(BoundBlockStatement node)
        {
            foreach (var statement in node.Statements)
            {
                EmitStatement(statement);
            }
        }

        private void EmitFunctions(ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functions)
        {
            foreach (var kvp in functions)
            {
                var declaration = kvp.Key;
                var body = kvp.Value;
                EmitFunction(declaration, body);
            }
        }

        private void EmitFunction(FunctionSymbol symbol, BoundBlockStatement body)
        {
            _emitHelper.StartEmitFunction(symbol);
            EmitBlockStatement(body);
            _emitHelper.EndEmitFunction();
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
            var slot = CurrentMethodFrame.GetOrCreateVariableSlot(node.Variable);
            Il.Emit(OpCodes.Stloc, slot);

            Il.Emit(OpCodes.Ldloc, slot);
            EmitSaveResult(node.Variable.Type);
        }

        private void EmitExpressionStatement(BoundExpressionStatement node)
        {
            EmitExpression(node.Expression);
            EmitSaveResult(node.Expression.Type);
        }

        private void EmitGotoStatement(BoundGotoStatement node)
        {
            CurrentMethodFrame.AddJump(OpCodes.Br_S, node.Label);
        }

        private void EmitConditionalGotoStatement(BoundConditionalGotoStatement node)
        {
            EmitExpression(node.Condition);
            var branchOpcode = node.JumpIfTrue ? OpCodes.Brtrue_S : OpCodes.Brfalse_S;
            CurrentMethodFrame.AddJump(branchOpcode, node.Label);
        }

        private void EmitLabelStatement(BoundLabelStatement node)
        {
            CurrentMethodFrame.MarkLabel(node.Label);
        }

        private void EmitSaveResult(TypeSymbol resultType)
        {
            if (resultType != TypeSymbol.Void)
            {
                Il.Emit(OpCodes.Box, _emitHelper.ImportReference(resultType));
            }

            Il.Emit(OpCodes.Stloc_0);
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
            Il.Emit(OpCodes.Ldc_I4, value ? 1 : 0);
        }

        private void EmitIntLiteral(int value)
        {
            Il.Emit(OpCodes.Ldc_I4, value);
        }

        private void EmitStringLiteral(string value)
        {
            Il.Emit(OpCodes.Ldstr, value);
        }

        private void EmitVariableExpression(BoundVariableExpression v)
        {
            var slot = CurrentMethodFrame.GetOrCreateVariableSlot(v.Variable);
            Il.Emit(OpCodes.Ldloc, slot);
        }

        private void EmitAssignmentExpression(BoundAssignmentExpression a)
        {
            EmitExpression(a.Expression);
            var slot = CurrentMethodFrame.GetOrCreateVariableSlot(a.Variable);
            Il.Emit(OpCodes.Stloc, slot);

            // push result of the expression back on the stack, since this expression also produces a value downstream
            Il.Emit(OpCodes.Ldloc, slot);
        }

        private void EmitUnaryExpression(BoundUnaryExpression u)
        {
            EmitExpression(u.Operand);

            switch (u.Op.Kind)
            {
                case BoundUnaryOperatorKind.Identity:
                    break;
                case BoundUnaryOperatorKind.Negation:
                    Il.Emit(OpCodes.Neg);
                    break;
                case BoundUnaryOperatorKind.LogicalNegation:
                    Il.Emit(OpCodes.Ldc_I4_0);
                    Il.Emit(OpCodes.Ceq);
                    break;
                case BoundUnaryOperatorKind.OnesComplement:
                    Il.Emit(OpCodes.Not);
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
                    Il.Emit(OpCodes.Add);
                    break;
                case BoundBinaryOperatorKind.Subtraction:
                    Il.Emit(OpCodes.Sub);
                    break;
                case BoundBinaryOperatorKind.Multiplication:
                    Il.Emit(OpCodes.Mul);
                    break;
                case BoundBinaryOperatorKind.Division:
                    Il.Emit(OpCodes.Div);
                    break;
                case BoundBinaryOperatorKind.BitwiseXor:
                    Il.Emit(OpCodes.Xor);
                    break;
                case BoundBinaryOperatorKind.LogicalAnd:
                case BoundBinaryOperatorKind.BitwiseAnd:
                    Il.Emit(OpCodes.And);
                    break;
                case BoundBinaryOperatorKind.LogicalOr:
                case BoundBinaryOperatorKind.BitwiseOr:
                    Il.Emit(OpCodes.Or);
                    break;
                case BoundBinaryOperatorKind.Equals:
                    Il.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.NotEquals:
                    Il.Emit(OpCodes.Ceq);
                    Il.Emit(OpCodes.Ldc_I4_0);
                    Il.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.Less:
                    Il.Emit(OpCodes.Clt);
                    break;
                case BoundBinaryOperatorKind.LessOrEquals:
                    Il.Emit(OpCodes.Cgt);
                    Il.Emit(OpCodes.Ldc_I4_0);
                    Il.Emit(OpCodes.Ceq);
                    break;
                case BoundBinaryOperatorKind.Greater:
                    Il.Emit(OpCodes.Cgt);
                    break;
                case BoundBinaryOperatorKind.GreaterOrEquals:
                    Il.Emit(OpCodes.Clt);
                    Il.Emit(OpCodes.Ldc_I4_0);
                    Il.Emit(OpCodes.Ceq);
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

            Il.Emit(OpCodes.Call, LookupFunction(node.Function));
        }

        private MethodReference LookupFunction(FunctionSymbol symbol)
        {
            var builtinFunctionWrapperMethod = BuiltinFunctionImplementations.LookupFunction(symbol);

            return builtinFunctionWrapperMethod != null
                ? _emitHelper.ImportReference(builtinFunctionWrapperMethod)
                : _emitHelper.ImportReference(symbol);
        }

        private void EmitConversionExpression(BoundConversionExpression node)
        {
            EmitExpression(node.Expression);

            var conversionFunctionMethodWrapper = BuiltinFunctionImplementations.LookupFunction(node);
            Il.Emit(OpCodes.Call, _emitHelper.ImportReference(conversionFunctionMethodWrapper));
        }
    }

    public static class BuiltinFunctionImplementations
    {
        internal static MethodInfo LookupFunction(BoundConversionExpression node)
        {
            string name = $"{ToCamelCase(node.Expression.Type)}To{ToCamelCase(node.Type)}";
            return typeof(BuiltinFunctionImplementations).GetMethod(name);
        }

        internal static MethodInfo LookupFunction(FunctionSymbol symbol)
        {
            return typeof(BuiltinFunctionImplementations).GetMethod(ToCamelCase(symbol.Name));
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