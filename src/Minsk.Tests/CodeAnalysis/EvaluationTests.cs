using System;
using System.Collections.Generic;
using Minsk.CodeAnalysis;
using Minsk.CodeAnalysis.Symbols;
using Minsk.CodeAnalysis.Syntax;
using Xunit;

namespace Minsk.Tests.CodeAnalysis
{
    public class EvaluationTests
    {
        [Theory]
        [FeatureToggledInlineData("1", 1)]
        [FeatureToggledInlineData("+1", 1)]
        [FeatureToggledInlineData("-1", -1)]
        [FeatureToggledInlineData("~1", -2)]
        [FeatureToggledInlineData("14 + 12", 26)]
        [FeatureToggledInlineData("12 - 3", 9)]
        [FeatureToggledInlineData("4 * 2", 8)]
        [FeatureToggledInlineData("9 / 3", 3)]
        [FeatureToggledInlineData("(10)", 10)]
        [FeatureToggledInlineData("12 == 3", false)]
        [FeatureToggledInlineData("3 == 3", true)]
        [FeatureToggledInlineData("12 != 3", true)]
        [FeatureToggledInlineData("3 != 3", false)]
        [FeatureToggledInlineData("3 < 4", true)]
        [FeatureToggledInlineData("5 < 4", false)]
        [FeatureToggledInlineData("4 <= 4", true)]
        [FeatureToggledInlineData("4 <= 5", true)]
        [FeatureToggledInlineData("5 <= 4", false)]
        [FeatureToggledInlineData("4 > 3", true)]
        [FeatureToggledInlineData("4 > 5", false)]
        [FeatureToggledInlineData("4 >= 4", true)]
        [FeatureToggledInlineData("5 >= 4", true)]
        [FeatureToggledInlineData("4 >= 5", false)]
        [FeatureToggledInlineData("1 | 2", 3)]
        [FeatureToggledInlineData("1 | 0", 1)]
        [FeatureToggledInlineData("1 & 3", 1)]
        [FeatureToggledInlineData("1 & 0", 0)]
        [FeatureToggledInlineData("1 ^ 0", 1)]
        [FeatureToggledInlineData("0 ^ 1", 1)]
        [FeatureToggledInlineData("1 ^ 3", 2)]
        [FeatureToggledInlineData("false == false", true)]
        [FeatureToggledInlineData("true == false", false)]
        [FeatureToggledInlineData("false != false", false)]
        [FeatureToggledInlineData("true != false", true)]
        [FeatureToggledInlineData("true && true", true)]
        [FeatureToggledInlineData("false || false", false)]
        [FeatureToggledInlineData("false | false", false)]
        [FeatureToggledInlineData("false | true", true)]
        [FeatureToggledInlineData("true | false", true)]
        [FeatureToggledInlineData("true | true", true)]
        [FeatureToggledInlineData("false & false", false)]
        [FeatureToggledInlineData("false & true", false)]
        [FeatureToggledInlineData("true & false", false)]
        [FeatureToggledInlineData("true & true", true)]
        [FeatureToggledInlineData("false ^ false", false)]
        [FeatureToggledInlineData("true ^ false", true)]
        [FeatureToggledInlineData("false ^ true", true)]
        [FeatureToggledInlineData("true ^ true", false)]
        [FeatureToggledInlineData("true", true)]
        [FeatureToggledInlineData("false", false)]
        [FeatureToggledInlineData("!true", false)]
        [FeatureToggledInlineData("!false", true)]
        [FeatureToggledInlineData("var a = 10", 10)]
        [FeatureToggledInlineData("\"test\"", "test")]
        [FeatureToggledInlineData("string(true)", "True")]
        [FeatureToggledInlineData("string(1)", "1")]
        [FeatureToggledInlineData("bool(\"true\")", true)]
        [FeatureToggledInlineData("int(\"1\")", 1)]
        [FeatureToggledInlineData("rnd(0)", 0)]
        [FeatureToggledInlineData("{ var a = 10 (a * a) }", 100)]
        [FeatureToggledInlineData("{ var a = 0 (a = 10) * a }", 100)]
        [FeatureToggledInlineData("{ var a = 0 if a == 0 a = 10 a }", 10)]
        [FeatureToggledInlineData("{ var a = 0 if a == 4 a = 10 a }", 0)]
        [FeatureToggledInlineData("{ var a = 0 if a == 0 a = 10 else a = 5 a }", 10)]
        [FeatureToggledInlineData("{ var a = 0 if a == 4 a = 10 else a = 5 a }", 5)]
        [FeatureToggledInlineData("{ var i = 10 var result = 0 while i > 0 { result = result + i i = i - 1} result }", 55)]
        [FeatureToggledInlineData("{ var result = 0 for i = 1 to 10 { result = result + i } result }", 55)]
        [FeatureToggledInlineData("{ var a = 10 for i = 1 to (a = a - 1) { } a }", 9)]
        [FeatureToggledInlineData("{ var a = 0 do a = a + 1 while a < 10 a}", 10)]
        public void Evaluator_Computes_CorrectValues(string text, object expectedValue, bool useJitting)
        {
            AssertValue(text, expectedValue, useJitting);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Evaluator_ContinueWith_CarriesOverVariablesBetweenCompilations(bool useJitting)
        {
            // arrange
            var firstSubmission = SyntaxTree.Parse("var a = 2");
            var secondSubmission = SyntaxTree.Parse("a * a");
            var variables = new Dictionary<VariableSymbol, object>();

            // act
            var compilation = new Compilation(firstSubmission);
            var firstResult = compilation.Evaluate(variables, useJitting);

            compilation = compilation.ContinueWith(secondSubmission);
            var secondResult = compilation.Evaluate(variables, useJitting);

            // assert
            Assert.Equal(2, firstResult.Value);
            Assert.Equal(4, secondResult.Value);
        }

        [Fact]
        public void Evaluator_VariableDeclaration_Reports_Redeclaration()
        {
            var text = @"
                {
                    var x = 10
                    var y = 100
                    {
                        var x = 10
                    }
                    var [x] = 5
                }
            ";

            var diagnostics = @"
                'x' is already declared.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_BlockStatement_NoInfiniteLoop()
        {
            var text = @"
                {
                [)][]
            ";

            var diagnostics = @"
                Unexpected token <CloseParenthesisToken>, expected <IdentifierToken>.
                Unexpected token <EndOfFileToken>, expected <CloseBraceToken>.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_IfStatement_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 0
                    if [10]
                        x = 10
                }
            ";

            var diagnostics = @"
                Cannot convert type 'int' to 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_WhileStatement_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 0
                    while [10]
                        x = 10
                }
            ";

            var diagnostics = @"
                Cannot convert type 'int' to 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_DoWhileStatement_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 0
                    do
                        x = 10
                    while [10]
                }
            ";

            var diagnostics = @"
                Cannot convert type 'int' to 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_ForStatement_Reports_CannotConvert_LowerBound()
        {
            var text = @"
                {
                    var result = 0
                    for i = [false] to 10
                        result = result + i
                }
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_ForStatement_Reports_CannotConvert_UpperBound()
        {
            var text = @"
                {
                    var result = 0
                    for i = 1 to [true]
                        result = result + i
                }
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NameExpression_Reports_Undefined()
        {
            var text = @"[x] * 10";

            var diagnostics = @"
                Variable 'x' doesn't exist.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_NameExpression_Reports_NoErrorForInsertedToken()
        {
            var text = @"1 + []";

            var diagnostics = @"
                Unexpected token <EndOfFileToken>, expected <IdentifierToken>.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_UnaryExpression_Reports_Undefined()
        {
            var text = @"[+]true";

            var diagnostics = @"
                Unary operator '+' is not defined for type 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_BinaryExpression_Reports_Undefined()
        {
            var text = @"10 [*] false";

            var diagnostics = @"
                Binary operator '*' is not defined for types 'int' and 'bool'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_AssignmentExpression_Reports_Undefined()
        {
            var text = @"[x] = 10";

            var diagnostics = @"
                Variable 'x' doesn't exist.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_AssignmentExpression_Reports_CannotAssign()
        {
            var text = @"
                {
                    let x = 10
                    x [=] 0
                }
            ";

            var diagnostics = @"
                Variable 'x' is read-only and cannot be assigned to.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_AssignmentExpression_Reports_CannotConvert()
        {
            var text = @"
                {
                    var x = 10
                    x = [true]
                }
            ";

            var diagnostics = @"
                Cannot convert type 'bool' to 'int'.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        [Fact]
        public void Evaluator_Variables_Can_Shadow_Functions()
        {
            var text = @"
                {
                    let print = 42
                    [print](""test"")
                }
            ";

            var diagnostics = @"
                Function 'print' doesn't exist.
            ";

            AssertDiagnostics(text, diagnostics);
        }

        private static void AssertValue(string text, object expectedValue, bool useJitting = false)
        {
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = new Compilation(syntaxTree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables, useJitting);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(expectedValue, result.Value);
        }

        private void AssertDiagnostics(string text, string diagnosticText)
        {
            var annotatedText = AnnotatedText.Parse(text);
            var syntaxTree = SyntaxTree.Parse(annotatedText.Text);
            var compilation = new Compilation(syntaxTree);
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());

            var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

            if (annotatedText.Spans.Length != expectedDiagnostics.Length)
                throw new Exception("ERROR: Must mark as many spans as there are expected diagnostics");

            Assert.Equal(expectedDiagnostics.Length, result.Diagnostics.Length);

            for (var i = 0; i < expectedDiagnostics.Length; i++)
            {
                var expectedMessage = expectedDiagnostics[i];
                var actualMessage = result.Diagnostics[i].Message;
                Assert.Equal(expectedMessage, actualMessage);

                var expectedSpan = annotatedText.Spans[i];
                var actualSpan = result.Diagnostics[i].Span;
                Assert.Equal(expectedSpan, actualSpan);
            }
        }
    }
}
