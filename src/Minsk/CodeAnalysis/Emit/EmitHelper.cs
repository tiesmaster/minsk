using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Hosting;
using Minsk.CodeAnalysis.Symbols;

using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis.Emit
{
    internal class EmittingMethodFrame
    {
        private readonly ModuleDefinition _moduleDefinition;

        private readonly Dictionary<VariableSymbol, int> _variables = new Dictionary<VariableSymbol, int>();
        private readonly List<(Instruction, BoundLabel)> _jumpPatchList = new List<(Instruction, BoundLabel)>();
        private readonly Dictionary<BoundLabel, Instruction> _labelMapping = new Dictionary<BoundLabel, Instruction>();
        private readonly Instruction _dummyJumpInstruction;

        private int _nextFreeVariableSlot;

        public MethodDefinition MethodDefinition { get; }
        public ILProcessor IlProcessor { get; }

        public IEnumerable<VariableDef> Variables => _variables.Select(kvp => new VariableDef(kvp.Key, kvp.Value));
        public TypeSystem TypeSystem => _moduleDefinition.TypeSystem;

        public static EmittingMethodFrame FromSymbol(FunctionSymbol symbol) => throw new NotImplementedException();

        public static EmittingMethodFrame FromHostMethodDefinition(
            HostMethodDefinition hostMethodDefinition,
            ModuleDefinition moduleDefinition)
        {
            var methodDefinition = new MethodDefinition(hostMethodDefinition.MethodName,
                MethodAttributes.Public | MethodAttributes.Static, moduleDefinition.TypeSystem.Object);

            methodDefinition.Parameters.Add(new ParameterDefinition(
                "variables",
                ParameterAttributes.None,
                moduleDefinition.ImportReference(typeof(object[]))));

            // first variable slot is the result variable
            var nextFreeVariableSlot = 1;

            var frame = new EmittingMethodFrame(moduleDefinition, methodDefinition, nextFreeVariableSlot);
            frame.AddResultVariable();

            return frame;
        }

        public EmittingMethodFrame(
            ModuleDefinition moduleDefinition,
            MethodDefinition methodDefinition,
            int nextFreeVariableSlot = 0)
        {
            _moduleDefinition = moduleDefinition;
            MethodDefinition = methodDefinition;

            _nextFreeVariableSlot = nextFreeVariableSlot;

            IlProcessor = methodDefinition.Body.GetILProcessor();

            _dummyJumpInstruction = IlProcessor.Create(OpCodes.Nop);
        }

        public TypeReference ImportReference(TypeSymbol typeSymbol)
        {
            var clrType = typeSymbol.ClrType;
            return _moduleDefinition.ImportReference(clrType);
        }

        public int GetOrCreateVariableSlot(VariableSymbol variable)
        {
            if (_variables.TryGetValue(variable, out var existingSlot))
            {
                return existingSlot;
            }
            var freeSlot = _nextFreeVariableSlot++;

            _variables[variable] = freeSlot;
            AddVariable(variable.Type);

            return freeSlot;
        }

        public void MarkLabel(BoundLabel label)
        {
            _labelMapping[label] = IlProcessor.Body.Instructions.LastOrDefault();
        }

        public void AddJump(OpCode jumpOpcode, BoundLabel jumpLabel)
        {
            var jumpInstruction = IlProcessor.Create(jumpOpcode, _dummyJumpInstruction);
            _jumpPatchList.Add((jumpInstruction, jumpLabel));

            IlProcessor.Append(jumpInstruction);
        }

        private void AddResultVariable()
        {
            MethodDefinition.Body.Variables.Add(new VariableDefinition(TypeSystem.Object));
        }

        private void AddVariable(TypeSymbol variableType)
        {
            MethodDefinition.Body.Variables.Add(new VariableDefinition(ImportReference(variableType)));
        }

        public void FinalizeMethod()
        {
            PatchupJumps();
        }

        private void PatchupJumps()
        {
            foreach (var (jump, label) in _jumpPatchList)
            {
                var targetInstruction = _labelMapping[label].Next;
                jump.Operand = targetInstruction;
            }
        }
    }

    internal class EmitHelper
    {
        private readonly HostMethodDefinition _hostingHostMethodDefinition;

        private readonly AssemblyDefinition _hostAssemblyDefinition;
        private readonly TypeDefinition _hostTypeDefinition;

        private readonly EmittingMethodFrame _hostMethodFrame;

        private EmittingMethodFrame _functionMethodFrame;

        public EmitHelper(HostMethodDefinition hostingHostMethodDefinition)
        {
            _hostingHostMethodDefinition = hostingHostMethodDefinition;

            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(
                    hostingHostMethodDefinition.AssemblyName,
                    new Version(1, 0, 0, 0)), hostingHostMethodDefinition.AssemblyName, ModuleKind.Dll);

            HostModule = _hostAssemblyDefinition.MainModule;

            _hostTypeDefinition = new TypeDefinition(null, hostingHostMethodDefinition.TypeName,
                TypeAttributes.Class | TypeAttributes.Public, TypeSystem.Object);

            HostModule.Types.Add(_hostTypeDefinition);

            _hostMethodFrame = EmittingMethodFrame.FromHostMethodDefinition(hostingHostMethodDefinition, HostModule);
            _hostTypeDefinition.Methods.Add(_hostMethodFrame.MethodDefinition);
        }

        private ModuleDefinition HostModule { get; }

        public EmittingMethodFrame CurrentMethodFrame => _functionMethodFrame ?? _hostMethodFrame;
        public TypeSystem TypeSystem => HostModule.TypeSystem;

        public HostMethod Finalize()
        {
            _hostMethodFrame.FinalizeMethod();
            return CreateHostMethod();
        }

        private HostMethod CreateHostMethod()
        {
            using (var ms = new MemoryStream())
            {
                _hostAssemblyDefinition.Write(ms);

                var peBytes = ms.ToArray();
                return new HostMethod(_hostingHostMethodDefinition, peBytes, _hostMethodFrame.Variables);
            }
        }

        public TypeReference ImportReference(TypeSymbol typeSymbol)
        {
            var clrType = typeSymbol.ClrType;
            return HostModule.ImportReference(clrType);
        }

        public MethodReference ImportReference(System.Reflection.MethodBase methodBase) => HostModule.ImportReference(methodBase);

        public void StartEmitFunction(FunctionSymbol symbol)
        {
            _functionMethodFrame = EmittingMethodFrame.FromSymbol(symbol);
        }

        public void EndEmitFunction()
        {
            _functionMethodFrame.FinalizeMethod();
            _hostTypeDefinition.Methods.Add(_functionMethodFrame.MethodDefinition);

            _functionMethodFrame = null;
        }
    }
}