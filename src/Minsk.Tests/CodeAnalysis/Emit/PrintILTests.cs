using System.IO;
using System.Text;

using Minsk.CodeAnalysis;
using Minsk.CodeAnalysis.Syntax;

using Xunit;

namespace Minsk.Tests.CodeAnalysis.Emit
{
    public class PrintILTests
    {
        [Fact]
        public void SimpleExpression()
        {
            var text = "1";
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = new Compilation(syntaxTree);

            var ms = new MemoryStream();
            var textWriter = new StreamWriter(ms);
            compilation.EmitIL(textWriter);

            textWriter.Flush();
            ms.Position = 0;
            var il = Encoding.UTF8.GetString(ms.ToArray());

            Assert.Equal(@"// Method begins at RVA 0x2050
// Code size 13 (0xd)
.maxstack 1
.locals (
	[0] object
)

IL_0000: ldc.i4 1
IL_0005: box [System.Private.CoreLib]System.Int32
IL_000a: stloc.0
IL_000b: ldloc.0
IL_000c: ret
", il);
        }
    }
}
