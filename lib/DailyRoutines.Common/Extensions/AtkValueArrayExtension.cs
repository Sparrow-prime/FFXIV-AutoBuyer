using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmenTools.Interop.Game.Models;

namespace DailyRoutines.Common.Extensions;

public static class AtkValueArrayExtension
{
    extension(AtkValueArray valueArray)
    {
        public static AtkValueArray FromString(string[] inputs)
        {
            var parsedObjects = new object[inputs.Length];

            for (var i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];

                if (string.IsNullOrWhiteSpace(input))
                {
                    parsedObjects[i] = string.Empty;
                    continue;
                }

                var expression = SyntaxFactory.ParseExpression(input);
                switch (expression)
                {
                    case LiteralExpressionSyntax literal:
                        parsedObjects[i] = literal.Token.Value ?? input;
                        break;
                    case PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax innerLiteral } unary when
                        unary.Kind() == SyntaxKind.UnaryMinusExpression:
                    {
                        var val = innerLiteral.Token.Value;
                        parsedObjects[i] = -(dynamic)val;
                        break;
                    }
                    default:
                        parsedObjects[i] = input;
                        break;
                }
            }

            return new(parsedObjects);
        }
    }

}
