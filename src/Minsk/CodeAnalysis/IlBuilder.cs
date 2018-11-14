using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal class IlBuilder
    {
        private readonly AssemblyDefinition _hostAssemblyDefinition;

        public IlBuilder()
        {
            var name = "HostAssembly";
            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);

            var hostModule = _hostAssemblyDefinition.MainModule;

            var hostTypeDefinition = new TypeDefinition(null, "HostType",
                TypeAttributes.Class | TypeAttributes.Public, hostModule.TypeSystem.Object);

            hostModule.Types.Add(hostTypeDefinition);

            var hostMethodDefinition = new MethodDefinition("HostMethod",
                MethodAttributes.Public | MethodAttributes.Static, hostModule.ImportReference(typeof(int)));

            hostTypeDefinition.Methods.Add(hostMethodDefinition);

            HostMethodIlProcessor = hostMethodDefinition.Body.GetILProcessor();
        }

        public ILProcessor HostMethodIlProcessor { get; private set; }

        public System.Reflection.Assembly Build()
        {
            using (var ms = new MemoryStream())
            {
                _hostAssemblyDefinition.Write(ms);

                var peBytes = ms.ToArray();
                var assembly = System.Reflection.Assembly.Load(peBytes);

                return assembly;
            }
        }
    }
}