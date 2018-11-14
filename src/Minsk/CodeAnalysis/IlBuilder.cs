using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Minsk.CodeAnalysis
{
    internal class IlBuilder
    {
        internal readonly ILProcessor _il;
        private AssemblyDefinition _hostAssemblyDefinition;

        public IlBuilder()
        {
            var name = "HostAssembly";
            _hostAssemblyDefinition = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);

            var hostModule = _hostAssemblyDefinition.MainModule;

            var hostTypeDefinition = new TypeDefinition(null, "HostType",
                Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, hostModule.TypeSystem.Object);

            hostModule.Types.Add(hostTypeDefinition);

            var hostMethodDefinition = new MethodDefinition("HostMethod",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, hostModule.ImportReference(typeof(int)));

            hostTypeDefinition.Methods.Add(hostMethodDefinition);

            _il = hostMethodDefinition.Body.GetILProcessor();
        }

        public Assembly FinalizeHostAssembly()
        {
            using (var ms = new MemoryStream())
            {
                _hostAssemblyDefinition.Write(ms);

                var peBytes = ms.ToArray();
                var assembly = Assembly.Load(peBytes);

                return assembly;
            }
        }
    }
}