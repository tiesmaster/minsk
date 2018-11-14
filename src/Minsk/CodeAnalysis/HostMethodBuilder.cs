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
        private readonly TypeReference _intType;

        private readonly Dictionary<VariableSymbol, int> _variables = new Dictionary<VariableSymbol, int>();
        private int _nextFreeVariableSlot = 1;

        public HostMethodBuilder()
        {
            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(_hostAssemblyName, new Version(1, 0, 0, 0)), _hostAssemblyName, ModuleKind.Dll);

            var hostModule = _hostAssemblyDefinition.MainModule;
            _intType = hostModule.ImportReference(typeof(int));

            var hostTypeDefinition = new TypeDefinition(null, _hostTypeName,
                TypeAttributes.Class | TypeAttributes.Public, hostModule.TypeSystem.Object);

            hostModule.Types.Add(hostTypeDefinition);


            _hostMethodDefinition = new MethodDefinition(_hostMethodName,
                MethodAttributes.Public | MethodAttributes.Static, _intType);

            hostTypeDefinition.Methods.Add(_hostMethodDefinition);

            HostMethodIlProcessor = _hostMethodDefinition.Body.GetILProcessor();

            AddVariable();
        }

        public ILProcessor HostMethodIlProcessor { get; }

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

        private void AddVariable()
        {
            _hostMethodDefinition.Body.Variables.Add(new VariableDefinition(_intType));
        }

        public int GetVariableSlot(VariableSymbol variable)
        {
            return _variables[variable];
        }
    }
}