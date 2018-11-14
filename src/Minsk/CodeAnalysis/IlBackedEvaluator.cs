using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Minsk.CodeAnalysis.Binding;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal sealed class IlBackedEvaluator
    {
        private readonly BoundStatement _root;
        private readonly Dictionary<VariableSymbol, object> _variables;

        private object _lastValue;
        private ILProcessor _il;

        public IlBackedEvaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables)
        {
            _root = root;
            _variables = variables;
        }

        public object Evaluate()
        {
            var myHelloWorldApp = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("HelloWorld", new Version(1, 0, 0, 0)), "HelloWorld", ModuleKind.Console);

            var module = myHelloWorldApp.MainModule;

            // create the program type and add it to the module
            var programType = new TypeDefinition("HelloWorld", "Program",
                Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);

            module.Types.Add(programType);

            // add an empty constructor
            var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig
                | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, module.TypeSystem.Void);

            // create the constructor's method body
            var il = ctor.Body.GetILProcessor();

            il.Append(il.Create(OpCodes.Ldarg_0));

            // call the base constructor
            il.Append(il.Create(OpCodes.Call, module.Import(typeof(object).GetConstructor(Array.Empty<Type>()))));

            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));

            programType.Methods.Add(ctor);

            // define the 'Main' method and add it to 'Program'
            var mainMethod = new MethodDefinition("Main",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.Import(typeof(int)));

            programType.Methods.Add(mainMethod);

            // add the 'args' parameter
            var argsParameter = new ParameterDefinition("args",
                Mono.Cecil.ParameterAttributes.None, module.Import(typeof(string[])));

            mainMethod.Parameters.Add(argsParameter);

            // create the method body
            _il = mainMethod.Body.GetILProcessor();

            // il.Append(il.Create(OpCodes.Nop));
            // il.Append(il.Create(OpCodes.Ldstr, "Hello World"));

            // var writeLineMethod = il.Create(OpCodes.Call,
            //     module.Import(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })));

            // // call the method
            // il.Append(writeLineMethod);

            // il.Append(il.Create(OpCodes.Nop));
            // il.Append(il.Create(OpCodes.Ret));

            // _il.Append(_il.Create(OpCodes.Ldc_I4_0));

            EmitStatement(_root);

            _il.Append(_il.Create(OpCodes.Ret));

            // set the entry point and save the module
            myHelloWorldApp.EntryPoint = mainMethod;
            // myHelloWorldApp.Write("HelloWorld.exe");
            using (var ms = new MemoryStream())
            {
                myHelloWorldApp.Write(ms);

                var peBytes = ms.ToArray();

                var assembly = Assembly.Load(peBytes);

                var p = assembly.GetType("HelloWorld.Program", throwOnError: true);

                var m = p.GetMethod("Main", BindingFlags.Static | BindingFlags.Public);

                var result = (int)m.Invoke(null, new object[] { new string[] { "test" } });
                // if (result != 0)
                // {
                //     throw new Exception();
                // }

                // throw new NotImplementedException("Finished up to here.");

                // EvaluateStatement(_root);
                // return _lastValue;

                return result;
            }
        }

        private void EmitStatement(BoundStatement node)
        {
            switch (node.Kind)
            {
                // case BoundNodeKind.BlockStatement:
                //     EmitBlockStatement((BoundBlockStatement)node);
                //     break;
                // case BoundNodeKind.VariableDeclaration:
                //     EmitVariableDeclaration((BoundVariableDeclaration)node);
                //     break;
                case BoundNodeKind.ExpressionStatement:
                    EmitExpressionStatement((BoundExpressionStatement)node);
                    break;
                default:
                    throw new Exception($"Unexpected node {node.Kind}");
            }
        }

        // private void EvaluateVariableDeclaration(BoundVariableDeclaration node)
        // {
        //     var value = EvaluateExpression(node.Initializer);
        //     _variables[node.Variable] = value;
        //     _lastValue = value;
        // }

        // private void EvaluateBlockStatement(BoundBlockStatement node)
        // {
        //     foreach (var statement in node.Statements)
        //         EvaluateStatement(statement);
        // }

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
                // case BoundNodeKind.VariableExpression:
                //     return EvaluateVariableExpression((BoundVariableExpression)node);
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

        // private object EvaluateVariableExpression(BoundVariableExpression v)
        // {
        //     return _variables[v.Variable];
        // }

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
                // case BoundUnaryOperatorKind.Negation:
                //     return -(int)operand;
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