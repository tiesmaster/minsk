using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Minsk.CodeAnalysis.Symbols;
using Minsk.CodeAnalysis.Binding;

namespace Minsk.CodeAnalysis
{
    internal class VariableDef
    {
        public VariableDef(VariableSymbol variable, int slot)
        {
            Variable = variable;
            Slot = slot;
        }

        public VariableSymbol Variable { get; }
        public int Slot { get; }
        public int VariableIndex => Slot - 1;
    }

    internal class HostMethodBuilder
    {
        private const string _hostAssemblyName = "HostAssembly";
        private const string _hostTypeName = "HostType";
        private const string _hostMethodName = "HostMethod";

        private readonly AssemblyDefinition _hostAssemblyDefinition;

        private readonly MethodDefinition _hostMethodDefinition;
        private readonly Instruction _dummyJumpInstruction;

        private readonly Dictionary<VariableSymbol, int> _variables = new Dictionary<VariableSymbol, int>();
        private readonly List<(Instruction, BoundLabel)> _jumpPatchList = new List<(Instruction, BoundLabel)>();
        private readonly Dictionary<BoundLabel, Instruction> _labelMapping = new Dictionary<BoundLabel, Instruction>();

        // first variable slot is the result variable
        private int _nextFreeVariableSlot = 1;

        public HostMethodBuilder()
        {
            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(_hostAssemblyName, new Version(1, 0, 0, 0)), _hostAssemblyName, ModuleKind.Dll);

            HostModule = _hostAssemblyDefinition.MainModule;

            var hostTypeDefinition = new TypeDefinition(null, _hostTypeName,
                TypeAttributes.Class | TypeAttributes.Public, TypeSystem.Object);

            HostModule.Types.Add(hostTypeDefinition);

            _hostMethodDefinition = new MethodDefinition(_hostMethodName,
                MethodAttributes.Public | MethodAttributes.Static, TypeSystem.Object);

            _hostMethodDefinition.Parameters.Add(
                new ParameterDefinition("variables", ParameterAttributes.None, HostModule.ImportReference(typeof(object[]))));

            hostTypeDefinition.Methods.Add(_hostMethodDefinition);

            HostMethodIlProcessor = _hostMethodDefinition.Body.GetILProcessor();

            _dummyJumpInstruction = HostMethodIlProcessor.Create(OpCodes.Nop);
            AddResultVariable();
        }

        public ILProcessor HostMethodIlProcessor { get; }
        private ModuleDefinition HostModule { get; }
        public TypeSystem TypeSystem => HostModule.TypeSystem;

        public IEnumerable<VariableDef> Variables => _variables.Select(kvp => new VariableDef(kvp.Key, kvp.Value));

        public HostMethod Build()
        {
            PatchupJumps();
            return CreateHostMethod();
        }

        private void PatchupJumps()
        {
            foreach (var (jump, label) in _jumpPatchList)
            {
                var targetInstruction = _labelMapping[label].Next;
                jump.Operand = targetInstruction;
            }
        }

        private HostMethod CreateHostMethod()
        {
            using (var ms = new MemoryStream())
            {
                _hostAssemblyDefinition.Write(ms);

                var peBytes = ms.ToArray();
                var assembly = System.Reflection.Assembly.Load(peBytes);

                return new HostMethod(assembly, _hostTypeName, _hostMethodName);
            }
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
            _labelMapping[label] = HostMethodIlProcessor.Body.Instructions.LastOrDefault();
        }

        public void AddJump(OpCode jumpOpcode, BoundLabel jumpLabel)
        {
            var jumpInstruction = HostMethodIlProcessor.Create(jumpOpcode, _dummyJumpInstruction);
            _jumpPatchList.Add((jumpInstruction, jumpLabel));

            HostMethodIlProcessor.Append(jumpInstruction);
        }

        public TypeReference ImportReference(TypeSymbol typeSymbol)
        {
            var clrType = ToClrType(typeSymbol);
            return HostModule.ImportReference(clrType);
        }

        public MethodReference ImportReference(System.Reflection.MethodBase methodBase) => HostModule.ImportReference(methodBase);

        public Type ToClrType(TypeSymbol typeSymbol)
        {
            if (typeSymbol == TypeSymbol.Bool)
            {
                return typeof(bool);
            }

            if (typeSymbol == TypeSymbol.Int)
            {
                return typeof(int);
            }

            if (typeSymbol == TypeSymbol.String)
            {
                return typeof(string);
            }

            throw new Exception($"Unsupported TypeSymbol given: {typeSymbol}");
        }

        private void AddResultVariable()
        {
            _hostMethodDefinition.Body.Variables.Add(new VariableDefinition(TypeSystem.Object));
        }

        private void AddVariable(TypeSymbol variableType)
        {
            _hostMethodDefinition.Body.Variables.Add(new VariableDefinition(ImportReference(variableType)));
        }
    }
}