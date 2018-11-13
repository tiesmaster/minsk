using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Syntax;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    public sealed class Compilation
    {
        private BoundGlobalScope _globalScope;

        public Compilation(SyntaxTree syntaxTree)
            : this(null, syntaxTree)
        {
            SyntaxTree = syntaxTree;
        }

        private Compilation(Compilation previous, SyntaxTree syntaxTree)
        {
            Previous = previous;
            SyntaxTree = syntaxTree;
        }

        public Compilation Previous { get; }
        public SyntaxTree SyntaxTree { get; }

        internal BoundGlobalScope GlobalScope
        {
            get
            {
                if (_globalScope == null)
                {
                    var globalScope = Binder.BindGlobalScope(Previous?.GlobalScope, SyntaxTree.Root);
                    Interlocked.CompareExchange(ref _globalScope, globalScope, null);
                }

                return _globalScope;
            }
        }

        public Compilation ContinueWith(SyntaxTree syntaxTree)
        {
            return new Compilation(this, syntaxTree);
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables)
        {
            var diagnostics = SyntaxTree.Diagnostics.Concat(GlobalScope.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var evaluator = new Evaluator(GlobalScope.Statement, variables);
            var value = evaluator.Evaluate();
            return new EvaluationResult(ImmutableArray<Diagnostic>.Empty, value);
        }

        public EmitResult Emit()
        {
            var diagnostics = SyntaxTree.Diagnostics.Concat(GlobalScope.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EmitResult(diagnostics);

            var emitBuilder = new EmitBuilder();
            var emitter = new Emitter(GlobalScope.Statement, emitBuilder.ILProcessor);

            emitter.Emit();
            emitBuilder.Build();

            return new EmitResult(ImmutableArray<Diagnostic>.Empty);
        }
    }

    public class EmitBuilder
    {
        AssemblyDefinition myHelloWorldApp;
        ModuleDefinition module;
        TypeDefinition programType;
        MethodDefinition ctor;
        MethodDefinition mainMethod;
        ILProcessor il;

        public EmitBuilder()
        {
            myHelloWorldApp = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("HelloWorld", new Version(1, 0, 0, 0)), "HelloWorld", ModuleKind.Console);
            module = myHelloWorldApp.MainModule;

            // create the program type and add it to the module
            programType = new TypeDefinition("HelloWorld", "Program",
                Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(programType);

            // add an empty constructor
            ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig
                | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, module.TypeSystem.Void);

            // create the constructor's method body
            il = ctor.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));

            // call the base constructor
            il.Append(il.Create(OpCodes.Call, module.Import(typeof(object).GetConstructor(Array.Empty<Type>()))));

            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));

            programType.Methods.Add(ctor);

            // define the 'Main' method and add it to 'Program'
            mainMethod = new MethodDefinition("Main",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, module.TypeSystem.Void);

            programType.Methods.Add(mainMethod);

            // add the 'args' parameter
            var argsParameter = new ParameterDefinition("args",
                Mono.Cecil.ParameterAttributes.None, module.Import(typeof(string[])));

            mainMethod.Parameters.Add(argsParameter);

            // create the method body
            il = mainMethod.Body.GetILProcessor();
        }

        public ILProcessor ILProcessor => il;

        public void Build()
        {
            // il.Append(il.Create(OpCodes.Nop));
            // il.Append(il.Create(OpCodes.Ldstr, "Hello World"));
            // il.Append(il.Create(OpCodes.Ldc_I4, 10));

            var writeLineMethod = il.Create(OpCodes.Call,
                module.Import(typeof(Console).GetMethod("WriteLine", new[] { typeof(int) })));

            // call the method
            il.Append(writeLineMethod);

            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));

            // set the entry point and save the module
            myHelloWorldApp.EntryPoint = mainMethod;
            myHelloWorldApp.Write("HelloWorld.exe");
        }
    }
}