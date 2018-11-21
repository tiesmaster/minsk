using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit.Sdk;

namespace Minsk.Tests.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class FeatureToggledInlineData : DataAttribute
    {
        readonly object[] data;

        public FeatureToggledInlineData(params object[] data)
        {
            this.data = data;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            return new[]
            {
                data.Append(true).ToArray(),
                data.Append(false).ToArray()
            };
        }
    }
}