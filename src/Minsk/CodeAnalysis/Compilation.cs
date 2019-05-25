using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Lowering;
using Minsk.CodeAnalysis.Symbols;
using Minsk.CodeAnalysis.Syntax;

namespace Minsk.CodeAnalysis
{
    public sealed class Compilation
    {
        private BoundGlobalScope _globalScope;

        public Compilation(SyntaxTree syntaxTree)
            : this(null, syntaxTree)
        {
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
        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables, bool useJitting = false)
        {
            var diagnostics = SyntaxTree.Diagnostics.Concat(GlobalScope.Diagnostics).ToImmutableArray();
            if (diagnostics.Any())
                return new EvaluationResult(diagnostics, null);

            var program = Binder.BindProgram(GlobalScope);
            if (program.Diagnostics.Any())
                return new EvaluationResult(program.Diagnostics.ToImmutableArray(), null);

            var value = Evaluate(program, variables, useJitting);
            return new EvaluationResult(ImmutableArray<Diagnostic>.Empty, value);
        }

        private static object Evaluate(BoundProgram program, Dictionary<VariableSymbol, object> variables, bool useJitting)
        {
            if (useJitting)
            {
                var evaluator = new IlBackedEvaluator(program, variables);
                return evaluator.Evaluate();
            }
            else
            {
                var evaluator = new Evaluator(program, variables);
                return evaluator.Evaluate();
            }
        }

        public void EmitTree(TextWriter writer)
        {
            var program = Binder.BindProgram(GlobalScope);
            program.Statement.WriteTo(writer);
        }

        public void EmitIL(TextWriter writer)
        {
            var statement = GetStatement();

            var evaluator = new IlBackedEvaluator(statement);

            evaluator.WriteTo(writer);
        }
    }
}