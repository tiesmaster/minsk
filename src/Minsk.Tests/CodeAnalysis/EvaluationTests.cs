using System.Collections.Generic;
using Minsk.CodeAnalysis;
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
        [FeatureToggledInlineData("14 + 12", 26)]
        [FeatureToggledInlineData("12 - 3", 9)]
        [FeatureToggledInlineData("4 * 2", 8)]
        [FeatureToggledInlineData("9 / 3", 3)]
        [FeatureToggledInlineData("(10)", 10)]
        [FeatureToggledInlineData("12 == 3", false)]
        [FeatureToggledInlineData("3 == 3", true)]
        [FeatureToggledInlineData("12 != 3", true)]
        [FeatureToggledInlineData("3 != 3", false)]
        [FeatureToggledInlineData("false == false", true)]
        [FeatureToggledInlineData("true == false", false)]
        [FeatureToggledInlineData("false != false", false)]
        [FeatureToggledInlineData("true != false", true)]
        [FeatureToggledInlineData("true && true", true)]
        [FeatureToggledInlineData("false || false", false)]
        [FeatureToggledInlineData("true", true)]
        [FeatureToggledInlineData("false", false)]
        [FeatureToggledInlineData("!true", false)]
        [FeatureToggledInlineData("!false", true)]
        [FeatureToggledInlineData("var a = 10", 10)]
        [FeatureToggledInlineData("{ var a = 10 (a * a) }", 100)]
        [FeatureToggledInlineData("{ var a = 0 (a = 10) * a }", 100)]
        public void JitEvaluator_Computes_CorrectValues(string text, object expectedValue, bool useJitting)
        {
            var syntaxTree = SyntaxTree.Parse(text);
            var compilation = new Compilation(syntaxTree);
            var variables = new Dictionary<VariableSymbol, object>();
            var result = compilation.Evaluate(variables, useJitting);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(expectedValue, result.Value);
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
    }
}