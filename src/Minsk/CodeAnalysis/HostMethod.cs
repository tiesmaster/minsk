using System.Reflection;

namespace Minsk.CodeAnalysis
{
    internal class HostMethod
    {
        private readonly Assembly _hostAssembly;
        private readonly string _hostTypeName;
        private readonly string _hostMethodName;

        public HostMethod(Assembly hostAssembly, string hostTypeName, string hostMethodName)
        {
            _hostAssembly = hostAssembly;
            _hostTypeName = hostTypeName;
            _hostMethodName = hostMethodName;
        }

        public int Invoke()
        {
            var hostType = _hostAssembly.GetType(_hostTypeName);
            var hostMethod = hostType.GetMethod(_hostMethodName, BindingFlags.Static | BindingFlags.Public);
            var result = (int)hostMethod.Invoke(null, null);

            return result;
        }
    }
}