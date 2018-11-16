using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal class HostMethodBuilder
    {
        private const string _hostAssemblyName = "HostAssembly";
        private const string _hostTypeName = "HostType";
        private const string _hostMethodName = "HostMethod";

        private readonly AssemblyDefinition _hostAssemblyDefinition;

        private readonly MethodDefinition _hostMethodDefinition;

        private readonly Dictionary<VariableSymbol, int> _variables = new Dictionary<VariableSymbol, int>();

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

            AddResultVariable();
        }

        public ILProcessor HostMethodIlProcessor { get; }
        public ModuleDefinition HostModule { get; }
        public TypeSystem TypeSystem => HostModule.TypeSystem;

        public Dictionary<VariableSymbol, int> Variables => _variables;

        public HostMethod Build()
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
            AddVariable();

            return freeSlot;
        }

        private void AddResultVariable()
        {
            _hostMethodDefinition.Body.Variables.Add(new VariableDefinition(TypeSystem.Object));
        }

        private void AddVariable()
        {
            _hostMethodDefinition.Body.Variables.Add(new VariableDefinition(TypeSystem.Int32));
        }
    }
}