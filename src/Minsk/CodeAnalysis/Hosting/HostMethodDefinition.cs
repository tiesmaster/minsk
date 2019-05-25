namespace Minsk.CodeAnalysis.Hosting
{
    public struct HostMethodDefinition
    {
        public HostMethodDefinition(string assemblyName, string typeName, string methodName)
        {
            AssemblyName = assemblyName;
            TypeName = typeName;
            MethodName = methodName;
        }

        public string AssemblyName { get; }
        public string TypeName { get; }
        public string MethodName { get; }
    }
}