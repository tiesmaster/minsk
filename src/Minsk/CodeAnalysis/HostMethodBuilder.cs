using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal class HostMethodBuilder
    {
        private readonly AssemblyDefinition _hostAssemblyDefinition;
        private const string _hostAssemblyName = "HostAssembly";
        private const string _hostTypeName = "HostType";
        private const string _hostMethodName = "HostMethod";

        public HostMethodBuilder()
        {
            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(_hostAssemblyName, new Version(1, 0, 0, 0)), _hostAssemblyName, ModuleKind.Dll);

            var hostModule = _hostAssemblyDefinition.MainModule;
            var intType = hostModule.ImportReference(typeof(int));

            var hostTypeDefinition = new TypeDefinition(null, _hostTypeName,
                TypeAttributes.Class | TypeAttributes.Public, hostModule.TypeSystem.Object);

            hostModule.Types.Add(hostTypeDefinition);

            var hostMethodDefinition = new MethodDefinition(_hostMethodName,
                MethodAttributes.Public | MethodAttributes.Static, intType);

            hostTypeDefinition.Methods.Add(hostMethodDefinition);

            HostMethodIlProcessor = hostMethodDefinition.Body.GetILProcessor();

            hostMethodDefinition.Body.Variables.Add(new VariableDefinition(intType));
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
    }
}