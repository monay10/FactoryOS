using System.Globalization;

namespace FactoryOS.Plugins.Workflow.Engine.Expressions;

/// <summary>
/// A compiled, side-effect-free workflow expression evaluated against a variable bag. Expressions drive
/// transition conditions and script assignments — they are <b>data</b>, never arbitrary code: the grammar
/// supports literals (numbers, single-quoted strings, <c>true</c>/<c>false</c>/<c>null</c>), variable
/// references, arithmetic (<c>+ - * /</c>), comparison (<c>== != &lt; &lt;= &gt; &gt;=</c>), logic
/// (<c>&amp;&amp; || !</c>) and parentheses. There is no member access, indexing or invocation.
/// </summary>
public sealed class WorkflowExpression
{
    private readonly Node _root;
    private readonly string _text;

    private WorkflowExpression(Node root, string text)
    {
        _root = root;
        _text = text;
    }

    /// <summary>Parses an expression string into a compiled <see cref="WorkflowExpression"/>.</summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The compiled expression.</returns>
    /// <exception cref="FormatException">Thrown when the expression is not valid.</exception>
    public static WorkflowExpression Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parser = new Parser(text);
        var node = parser.ParseExpression();
        parser.ExpectEnd();
        return new WorkflowExpression(node, text);
    }

    /// <summary>Evaluates the expression to a value.</summary>
    /// <param name="variables">The variables in scope.</param>
    /// <returns>The evaluated value (a <see cref="decimal"/>, <see cref="string"/>, <see cref="bool"/> or <see langword="null"/>).</returns>
    public object? Evaluate(IReadOnlyDictionary<string, object?> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return _root.Evaluate(variables);
    }

    /// <summary>Evaluates the expression as a boolean condition.</summary>
    /// <param name="variables">The variables in scope.</param>
    /// <returns>The truthiness of the evaluated value.</returns>
    public bool EvaluateBoolean(IReadOnlyDictionary<string, object?> variables) => ToBoolean(Evaluate(variables));

    /// <inheritdoc />
    public override string ToString() => _text;

    /// <summary>Coerces a value to a boolean: booleans as-is, non-zero numbers and non-empty strings are true, null is false.</summary>
    /// <param name="value">The value to coerce.</param>
    /// <returns>The boolean interpretation.</returns>
    public static bool ToBoolean(object? value) => value switch
    {
        null => false,
        bool boolean => boolean,
        decimal number => number != 0,
        string text => bool.TryParse(text, out var parsed) ? parsed : text.Length > 0,
        _ => true,
    };

    private abstract class Node
    {
        public abstract object? Evaluate(IReadOnlyDictionary<string, object?> variables);
    }

    private sealed class LiteralNode(object? value) : Node
    {
        public override object? Evaluate(IReadOnlyDictionary<string, object?> variables) => value;
    }

    private sealed class VariableNode(string name) : Node
    {
        public override object? Evaluate(IReadOnlyDictionary<string, object?> variables) =>
            variables.TryGetValue(name, out var value) ? Normalize(value) : null;
    }

    private sealed class UnaryNode(char op, Node operand) : Node
    {
        public override object? Evaluate(IReadOnlyDictionary<string, object?> variables)
        {
            var value = operand.Evaluate(variables);
            return op == '!' ? !ToBoolean(value) : -ToNumber(value);
        }
    }

    private sealed class BinaryNode(string op, Node left, Node right) : Node
    {
        public override object? Evaluate(IReadOnlyDictionary<string, object?> variables)
        {
            if (op == "&&")
            {
                return ToBoolean(left.Evaluate(variables)) && ToBoolean(right.Evaluate(variables));
            }

            if (op == "||")
            {
                return ToBoolean(left.Evaluate(variables)) || ToBoolean(right.Evaluate(variables));
            }

            var l = left.Evaluate(variables);
            var r = right.Evaluate(variables);

            return op switch
            {
                "==" => AreEqual(l, r),
                "!=" => !AreEqual(l, r),
                "<" => Compare(l, r) < 0,
                "<=" => Compare(l, r) <= 0,
                ">" => Compare(l, r) > 0,
                ">=" => Compare(l, r) >= 0,
                "+" => Add(l, r),
                "-" => ToNumber(l) - ToNumber(r),
                "*" => ToNumber(l) * ToNumber(r),
                "/" => ToNumber(l) / ToNumber(r),
                _ => throw new InvalidOperationException($"Unknown operator '{op}'."),
            };
        }

        private static object Add(object? l, object? r) =>
            l is string || r is string
                ? string.Concat(Stringify(l), Stringify(r))
                : ToNumber(l) + ToNumber(r);

        private static bool AreEqual(object? l, object? r)
        {
            if (l is null || r is null)
            {
                return l is null && r is null;
            }

            if (l is decimal || r is decimal)
            {
                return ToNumber(l) == ToNumber(r);
            }

            return Equals(l, r);
        }

        private static int Compare(object? l, object? r) =>
            l is string ls && r is string rs
                ? string.CompareOrdinal(ls, rs)
                : ToNumber(l).CompareTo(ToNumber(r));
    }

    private static object? Normalize(object? value) => value switch
    {
        null or bool or string or decimal => value,
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double =>
            Convert.ToDecimal(value, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static decimal ToNumber(object? value) => value switch
    {
        null => 0m,
        decimal number => number,
        bool boolean => boolean ? 1m : 0m,
        string text => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m,
        _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
    };

    private static string Stringify(object? value) => value switch
    {
        null => string.Empty,
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        _ => value.ToString() ?? string.Empty,
    };

    private sealed class Parser(string text)
    {
        private readonly string _text = text;
        private int _position;

        public Node ParseExpression() => ParseOr();

        public void ExpectEnd()
        {
            SkipWhitespace();
            if (_position != _text.Length)
            {
                throw new FormatException($"Unexpected character at position {_position} in '{_text}'.");
            }
        }

        private Node ParseOr()
        {
            var node = ParseAnd();
            while (Match("||"))
            {
                node = new BinaryNode("||", node, ParseAnd());
            }

            return node;
        }

        private Node ParseAnd()
        {
            var node = ParseEquality();
            while (Match("&&"))
            {
                node = new BinaryNode("&&", node, ParseEquality());
            }

            return node;
        }

        private Node ParseEquality()
        {
            var node = ParseComparison();
            while (true)
            {
                if (Match("==")) { node = new BinaryNode("==", node, ParseComparison()); }
                else if (Match("!=")) { node = new BinaryNode("!=", node, ParseComparison()); }
                else { return node; }
            }
        }

        private Node ParseComparison()
        {
            var node = ParseAdditive();
            while (true)
            {
                if (Match("<=")) { node = new BinaryNode("<=", node, ParseAdditive()); }
                else if (Match(">=")) { node = new BinaryNode(">=", node, ParseAdditive()); }
                else if (Match("<")) { node = new BinaryNode("<", node, ParseAdditive()); }
                else if (Match(">")) { node = new BinaryNode(">", node, ParseAdditive()); }
                else { return node; }
            }
        }

        private Node ParseAdditive()
        {
            var node = ParseMultiplicative();
            while (true)
            {
                if (Match("+")) { node = new BinaryNode("+", node, ParseMultiplicative()); }
                else if (Match("-")) { node = new BinaryNode("-", node, ParseMultiplicative()); }
                else { return node; }
            }
        }

        private Node ParseMultiplicative()
        {
            var node = ParseUnary();
            while (true)
            {
                if (Match("*")) { node = new BinaryNode("*", node, ParseUnary()); }
                else if (Match("/")) { node = new BinaryNode("/", node, ParseUnary()); }
                else { return node; }
            }
        }

        private Node ParseUnary()
        {
            if (Match("!")) { return new UnaryNode('!', ParseUnary()); }
            if (Match("-")) { return new UnaryNode('-', ParseUnary()); }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            SkipWhitespace();
            if (_position >= _text.Length)
            {
                throw new FormatException($"Unexpected end of expression '{_text}'.");
            }

            var current = _text[_position];
            if (current == '(')
            {
                _position++;
                var node = ParseOr();
                SkipWhitespace();
                if (_position >= _text.Length || _text[_position] != ')')
                {
                    throw new FormatException($"Expected ')' in '{_text}'.");
                }

                _position++;
                return node;
            }

            if (current == '\'')
            {
                return ParseString();
            }

            if (char.IsDigit(current))
            {
                return ParseNumber();
            }

            if (char.IsLetter(current) || current == '_')
            {
                return ParseIdentifier();
            }

            throw new FormatException($"Unexpected character '{current}' at position {_position} in '{_text}'.");
        }

        private Node ParseString()
        {
            _position++; // opening quote
            var start = _position;
            while (_position < _text.Length && _text[_position] != '\'')
            {
                _position++;
            }

            if (_position >= _text.Length)
            {
                throw new FormatException($"Unterminated string in '{_text}'.");
            }

            var value = _text[start.._position];
            _position++; // closing quote
            return new LiteralNode(value);
        }

        private Node ParseNumber()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
            {
                _position++;
            }

            var slice = _text[start.._position];
            return new LiteralNode(decimal.Parse(slice, NumberStyles.Any, CultureInfo.InvariantCulture));
        }

        private Node ParseIdentifier()
        {
            var start = _position;
            while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] is '_' or '.'))
            {
                _position++;
            }

            var name = _text[start.._position];
            return name switch
            {
                "true" => new LiteralNode(true),
                "false" => new LiteralNode(false),
                "null" => new LiteralNode(null),
                _ => new VariableNode(name),
            };
        }

        private bool Match(string token)
        {
            SkipWhitespace();
            if (_position + token.Length > _text.Length
                || string.CompareOrdinal(_text, _position, token, 0, token.Length) != 0)
            {
                return false;
            }

            // Guard against matching a prefix operator (e.g. '<' inside '<=').
            if (token is "<" or ">" && _position + 1 < _text.Length && _text[_position + 1] == '=')
            {
                return false;
            }

            if (token is "!" && _position + 1 < _text.Length && _text[_position + 1] == '=')
            {
                return false;
            }

            _position += token.Length;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }
    }
}
