﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Grimoire
{
    /// <summary>
    ///     Represents an EBNF grammar document
    /// </summary>
    /// <remarks>This class implements value semantics.</remarks>
    internal class EbnfDocument : IEquatable<EbnfDocument>, ICloneable
    {
        /// <summary>
        ///     Indicates the productions in the EBNF grammar document
        /// </summary>
        public IDictionary<string, EbnfProduction> Productions { get; } = new ListDictionary<string, EbnfProduction>();

        /// <summary>
        ///     Gets or sets the starting production of the grammar document
        /// </summary>
        /// <remarks>This property employs the "start" grammar attribute.</remarks>
        public string StartProduction
        {
            get
            {
                foreach (var prod in Productions)
                {
                    object b;
                    if (prod.Value.Attributes.TryGetValue("start", out b) && b is bool && (bool)b)
                        return prod.Key;
                }

                return Productions.First().Key;
            }
            set
            {
                if (!Productions.ContainsKey(value))
                    throw new ArgumentException("The value must be a non-terminal and present in the grammar.");
                foreach (var prod in Productions)
                    if (Equals(value, prod.Key))
                        prod.Value.Attributes["start"] = true;
                    else
                        prod.Value.Attributes.Remove("start");
            }
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public bool Equals(EbnfDocument rhs)
        {
            if (ReferenceEquals(rhs, this)) return true;
            if (ReferenceEquals(null, rhs)) return false;
            if (rhs.Productions.Count == Productions.Count) return false;
            using (var e = Productions.GetEnumerator())
            {
                using (var e2 = rhs.Productions.GetEnumerator())
                {
                    while (e.MoveNext())
                    {
                        if (!e2.MoveNext())
                            return false;
                        var x = e.Current;
                        var y = e2.Current;
                        if (!Equals(x.Key, y.Key))
                            return false;
                        if (!Equals(x.Value, y.Value))
                            return false;
                    }

                    if (e2.MoveNext())
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EbnfDocument);
        }

        public override int GetHashCode()
        {
            var result = 0;
            using (var e = Productions.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    if (null != e.Current.Key)
                        result ^= e.Current.Key.GetHashCode();
                    if (null != e.Current.Value)
                        result ^= e.Current.Value.GetHashCode();
                }
            }

            return result;
        }

        public static bool operator ==(EbnfDocument lhs, EbnfDocument rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(EbnfDocument lhs, EbnfDocument rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return false;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return true;
            return !lhs.Equals(rhs);
        }

        public EbnfDocument Clone()
        {
            var result = new EbnfDocument();
            foreach (var prod in Productions)
                result.Productions.Add(prod.Key, prod.Value.Clone());
            return result;
        }

        private void _VisitFetchTerminals(EbnfExpression expr, HashSet<EbnfExpression> terms)
        {
            var l = expr as EbnfLiteralExpression;
            if (null != l)
            {
                terms.Add(l);
                return;
            }

            var r = expr as EbnfRegexExpression;
            if (null != r)
            {
                terms.Add(r);
                return;
            }

            var u = expr as EbnfUnaryExpression;
            if (null != u)
            {
                _VisitFetchTerminals(u.Expression, terms);
                return;
            }

            var b = expr as EbnfBinaryExpression;
            if (null != b)
            {
                _VisitFetchTerminals(b.Left, terms);
                _VisitFetchTerminals(b.Right, terms);
            }
        }

        public IList<EbnfMessage> DeclareImplicitTerminals()
        {
            var result = new List<EbnfMessage>();
            var terms = new HashSet<EbnfExpression>();
            var done = new HashSet<EbnfExpression>();
            foreach (var prod in Productions)
            {
                _VisitFetchTerminals(prod.Value.Expression, terms);
                if (prod.Value.Expression.IsTerminal)
                    done.Add(prod.Value.Expression);
            }

            foreach (var term in terms)
                if (!done.Contains(term))
                {
                    var prod = new EbnfProduction();
                    prod.Expression = ((ICloneable)term).Clone() as EbnfExpression;
                    var newId = _GetImplicitTermId();
                    Productions.Add(newId, prod);
                    result.Add(new EbnfMessage(EbnfErrorLevel.Message, -1, "Terminal was implicitly declared.",
                        term.Line, term.Column, term.Position));
                }

            return result;
        }

        public string GetIdForExpression(EbnfExpression expression)
        {
            foreach (var prod in Productions)
                if (Equals(prod.Value.Expression, expression))
                    return prod.Key;
            return null;
        }

        private void _ValidateExpression(EbnfExpression expr, IDictionary<string, int> refCounts,
            IList<EbnfMessage> messages)
        {
            var l = expr as EbnfLiteralExpression;
            if (null != l)
            {
                var i = GetIdForExpression(l);
                // don't count itself. only things just like itself
                if (null != i && !ReferenceEquals(Productions[i].Expression, l))
                    refCounts[i] += 1;
            }

            var rx = expr as EbnfRegexExpression;
            if (null != rx)
            {
                try
                {
                    FA.Parse(rx.Value);
                }
                catch (ExpectingException)
                {
                    messages.Add(
                        new EbnfMessage(
                            EbnfErrorLevel.Error, 12,
                            "Invalid regular expression",
                            expr.Line, expr.Column, expr.Position));
                }

                var i = GetIdForExpression(rx);
                if (null != i && !ReferenceEquals(Productions[i].Expression, l))
                    refCounts[i] += 1;
            }

            var r = expr as EbnfRefExpression;
            if (null != r)
            {
                int rc;
                if (null == r.Symbol)
                {
                    messages.Add(
                        new EbnfMessage(
                            EbnfErrorLevel.Error, 4,
                            "Null reference expression",
                            expr.Line, expr.Column, expr.Position));
                    return;
                }

                if (!refCounts.TryGetValue(r.Symbol, out rc))
                {
                    messages.Add(
                        new EbnfMessage(
                            EbnfErrorLevel.Error, 1,
                            string.Concat(
                                "Reference to undefined symbol \"",
                                r.Symbol,
                                "\""),
                            expr.Line, expr.Column, expr.Position));
                    return;
                }

                refCounts[r.Symbol] = rc + 1;
                return;
            }

            var b = expr as EbnfBinaryExpression;
            if (null != b)
            {
                if (null == b.Left && null == b.Right)
                {
                    messages.Add(
                        new EbnfMessage(
                            EbnfErrorLevel.Warning, 3,
                            "Nil expression",
                            expr.Line, expr.Column, expr.Position));
                    return;
                }

                _ValidateExpression(b.Left, refCounts, messages);
                _ValidateExpression(b.Right, refCounts, messages);
                return;
            }

            var u = expr as EbnfUnaryExpression;
            if (null != u)
            {
                if (null == u.Expression)
                {
                    messages.Add(
                        new EbnfMessage(
                            EbnfErrorLevel.Warning, 3,
                            "Nil expression",
                            expr.Line, expr.Column, expr.Position));
                    return;
                }

                _ValidateExpression(u.Expression, refCounts, messages);
            }
        }

        public IList<EbnfMessage> Validate(bool throwIfErrors = false)
        {
            var result = new List<EbnfMessage>();
            var refCounts = new Dictionary<string, int>(EqualityComparer<string>.Default);
            foreach (var prod in Productions)
                refCounts.Add(prod.Key, 0);
            foreach (var prod in Productions) _ValidateExpression(prod.Value.Expression, refCounts, result);
            foreach (var rc in refCounts)
                if (0 == rc.Value)
                {
                    var prod = Productions[rc.Key];
                    object o;
                    var isHidden = prod.Attributes.TryGetValue("hidden", out o) && o is bool && (bool)o;
                    if (!isHidden && !Equals(rc.Key, StartProduction))
                        result.Add(new EbnfMessage(EbnfErrorLevel.Warning, 2,
                            string.Concat("Unreferenced production \"", rc.Key, "\""),
                            prod.Line, prod.Column, prod.Position));
                }

            if (throwIfErrors)
                EbnfException.ThrowIfErrors(result);
            return result;
        }

        public IList<EbnfMessage> Prepare(bool throwIfErrors = true)
        {
            var result = new List<EbnfMessage>();
            var msgs = Validate();
            result.AddRange(msgs);
            var hasError = false;
            foreach (var msg in msgs)
                if (EbnfErrorLevel.Error == msg.ErrorLevel)
                {
                    hasError = true;
                    break;
                }

            if (!hasError)
                result.AddRange(DeclareImplicitTerminals());
            if (throwIfErrors)
                EbnfException.ThrowIfErrors(result);
            return result;
        }

        private string _GetImplicitTermId()
        {
            var result = "implicit";
            var i = 2;
            while (Productions.ContainsKey(result))
            {
                result = string.Concat("implicit", i.ToString());
                ++i;
            }

            return result;
        }

        public FA ToLexer(ISymbolResolver resolver)
        {
            var fas = new List<FA>();
            foreach (var prod in Productions)
            {
                var exp = prod.Value.Expression;
                FA fa = null;
                if (exp.IsTerminal)
                {
                    var l = exp as EbnfLiteralExpression;
                    if (null != l)
                        fa = FA.Literal(l.Value, resolver.GetSymbolId(prod.Key));
                    var r = exp as EbnfRegexExpression;
                    if (null != r)
                        fa = FA.Parse(r.Value, resolver.GetSymbolId(prod.Key));
                    Debug.Assert(null != fa, "Unsupported terminal expression type.");
                    fas.Add(fa);
                }
            }

            return FA.Lexer(fas);
        }

        public Cfg ToCfg()
        {
            var result = new Cfg();
            foreach (var prod in Productions)
            {
                if (!prod.Value.Expression.IsTerminal)
                {
                    var ll = prod.Value.Expression.ToDisjunctions(this, result);
                    foreach (var l in ll)
                        result.Rules.Add(new CfgRule(prod.Key, l));
                }

                IDictionary<string, object> attrs = null;
                if (0 < prod.Value.Attributes.Count)
                {
                    attrs = new Dictionary<string, object>();
                    result.AttributeSets.Add(prod.Key, attrs);
                    foreach (var attr in prod.Value.Attributes)
                        attrs.Add(attr.Key, attr.Value);
                }
            }

            return result;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var prod in Productions)
            {
                sb.Append(prod.Key);
                sb.AppendLine(prod.Value.ToString());
            }

            return sb.ToString();
        }

        public static EbnfDocument ReadFrom(string filename)
        {
            using (var pc = ParseContext.CreateFromFile(filename))
            {
                return Parse(pc);
            }
        }

        public static EbnfDocument ReadFrom(TextReader reader)
        {
            return Parse(ParseContext.Create(reader));
        }

        public static EbnfDocument Parse(IEnumerable<char> @string)
        {
            return Parse(ParseContext.Create(@string));
        }

        public static EbnfDocument Parse2(ParseContext pc)
        {
            var doc = new EbnfDocument();
            var parser = new EbnfParser(pc);
            while (parser.Read())
                if (EbnfParser.production == parser.SymbolId)
                    _ReadProduction(doc, parser);
            return doc;
        }

        private static void _ReadProduction(EbnfDocument doc, EbnfParser parser)
        {
            parser.Read();
            var id = parser.Value;
            var prod = new EbnfProduction();
            parser.Read();
            if (EbnfParser.lt == parser.SymbolId)
            {
                parser.Read();
                _ReadAttributes(prod.Attributes, parser);
                parser.Read();
            }

            parser.Read();
            prod.Expression = _ReadExpressions(parser);
        }

        private static EbnfExpression _ReadExpressions(EbnfParser parser)
        {
            EbnfExpression result = null;
            throw new NotImplementedException();
            return result;
        }

        private static void _ReadAttributes(IDictionary<string, object> attrs, EbnfParser parser)
        {
            parser.Read();
            while (EbnfParser.attribute == parser.SymbolId)
            {
                parser.Read();
                var id = parser.Value;
                parser.Read();
                object val = true;
                if (EbnfParser.eq == parser.SymbolId)
                {
                    parser.Read();
                    parser.Read();
                    switch (parser.SymbolId)
                    {
                        case EbnfParser.identifier:
                            if ("null" == parser.Value)
                                val = null;
                            else if ("true" == parser.Value)
                                val = true;
                            else if ("false" == parser.Value)
                                val = false;
                            else
                                throw new ExpectingException("Expecting true, false, or null.");
                            break;
                        case EbnfParser.integer:
                            val = int.Parse(parser.Value);
                            break;
                        case EbnfParser.literal:
                            val = ParseContext.Create(parser.Value).ParseJsonString();
                            break;
                    }

                    parser.Read();
                }

                attrs.Add(id, val);
                if (EbnfParser.comma == parser.SymbolId)
                    parser.Read();
            }
        }

        public static EbnfDocument Parse(ParseContext pc)
        {
            var doc = new EbnfDocument();
            while (-1 != pc.Current)
            {
                _ParseProduction(doc, pc);
                pc.TrySkipCCommentsAndWhiteSpace();
            }

            return doc;
        }

        private static void _ParseProduction(EbnfDocument doc, ParseContext pc)
        {
            pc.TrySkipCCommentsAndWhiteSpace();
            var line = pc.Line;
            var column = pc.Column;
            var position = pc.Position;
            var id = _ParseIdentifier(pc);
            pc.TrySkipCCommentsAndWhiteSpace();
            var prod = doc.Productions.TryGetValue(id);
            if (null == prod)
            {
                prod = new EbnfProduction();
                doc.Productions.Add(id, prod);
            }

            if ('<' == pc.Current)
            {
                _ParseAttributes(doc, id, prod, pc);
                pc.TrySkipCCommentsAndWhiteSpace();
            }

            pc.Expecting('=');
            pc.Advance();
            pc.Expecting();
            var expr = _ParseExpression(doc, pc);
            pc.TrySkipCCommentsAndWhiteSpace();
            pc.Expecting(';');
            pc.Advance();
            pc.TrySkipCCommentsAndWhiteSpace();
            // transform this into an OrExpression with the previous
            if (null != prod.Expression)
                prod.Expression = new EbnfOrExpression(prod.Expression, expr);
            else
                prod.Expression = expr;
            prod.SetPositionInfo(line, column, position);
        }

        private static EbnfExpression _ParseExpression(EbnfDocument doc, ParseContext pc)
        {
            EbnfExpression current = null;
            EbnfExpression e;
            long position;
            int line;
            int column;
            pc.TrySkipCCommentsAndWhiteSpace();
            position = pc.Position;
            line = pc.Line;
            column = pc.Column;
            while (-1 != pc.Current && ']' != pc.Current && ')' != pc.Current && '}' != pc.Current && ';' != pc.Current)
            {
                pc.TrySkipCCommentsAndWhiteSpace();
                position = pc.Position;
                line = pc.Line;
                column = pc.Column;
                switch (pc.Current)
                {
                    case '|':
                        pc.Advance();
                        current = new EbnfOrExpression(current, _ParseExpression(doc, pc));
                        current.SetPositionInfo(line, column, position);
                        break;
                    case '(':
                        pc.Advance();
                        e = _ParseExpression(doc, pc);
                        current.SetPositionInfo(line, column, position);
                        pc.Expecting(')');
                        pc.Advance();
                        e.SetPositionInfo(line, column, position);
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        break;
                    case '[':
                        pc.Advance();
                        e = new EbnfOptionalExpression(_ParseExpression(doc, pc));
                        e.SetPositionInfo(line, column, position);
                        pc.TrySkipCCommentsAndWhiteSpace();
                        pc.Expecting(']');
                        pc.Advance();
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        break;
                    case '{':
                        pc.Advance();
                        e = new EbnfRepeatExpression(_ParseExpression(doc, pc));
                        e.SetPositionInfo(line, column, position);
                        pc.TrySkipCCommentsAndWhiteSpace();
                        pc.Expecting('}');
                        pc.Advance();
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        break;
                    case '\"':
                        e = new EbnfLiteralExpression(pc.ParseJsonString());
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        e.SetPositionInfo(line, column, position);
                        break;
                    case '\'':
                        pc.Advance();
                        pc.ClearCapture();
                        pc.TryReadUntil('\'', '\\', false);
                        pc.Expecting('\'');
                        pc.Advance();
                        e = new EbnfRegexExpression(pc.Capture);
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        e.SetPositionInfo(line, column, position);
                        break;
                    case ';':
                    case ']':
                    case ')':
                    case '}':
                        return current;
                    default:
                        e = new EbnfRefExpression(_ParseIdentifier(pc));
                        if (null == current)
                            current = e;
                        else
                            current = new EbnfConcatExpression(current, e);
                        e.SetPositionInfo(line, column, position);
                        break;
                }
            }

            pc.TrySkipCCommentsAndWhiteSpace();
            return current;
        }

        private static void _ParseAttribute(EbnfDocument doc, string id, EbnfProduction prod, ParseContext pc)
        {
            pc.TrySkipCCommentsAndWhiteSpace();
            var attrid = _ParseIdentifier(pc);
            pc.TrySkipCCommentsAndWhiteSpace();
            pc.Expecting('=', '>', ',');
            object val = true;
            if ('=' == pc.Current)
            {
                pc.Advance();
                val = pc.ParseJsonValue();
            }

            pc.Expecting(',', '>');
            prod.Attributes[attrid] = val;
            pc.TrySkipCCommentsAndWhiteSpace();
        }

        private static void _ParseAttributes(EbnfDocument doc, string id, EbnfProduction prod, ParseContext pc)
        {
            pc.TrySkipCCommentsAndWhiteSpace();
            pc.Expecting('<');
            pc.Advance();
            while (-1 != pc.Current && '>' != pc.Current)
            {
                _ParseAttribute(doc, id, prod, pc);
                pc.TrySkipCCommentsAndWhiteSpace();
                pc.Expecting(',', '>');
                if (',' == pc.Current)
                    pc.Advance();
            }

            pc.Expecting('>');
            pc.Advance();
            pc.TrySkipCCommentsAndWhiteSpace();
        }

        private static string _ParseIdentifier(ParseContext pc)
        {
            pc.TrySkipCCommentsAndWhiteSpace();
            if (-1 == pc.Current)
            {
                pc.Expecting();
                return null;
            }

            var l = pc.CaptureBuffer.Length;
            if ('_' != pc.Current && !char.IsLetter((char)pc.Current))
                pc.Expecting("ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz".ToCharArray().Convert<int>()
                    .ToArray());
            pc.CaptureCurrent();
            while (-1 != pc.Advance() &&
                   ('_' == pc.Current || '-' == pc.Current || char.IsLetterOrDigit((char)pc.Current)))
                pc.CaptureCurrent();
            pc.TrySkipCCommentsAndWhiteSpace();
            return pc.GetCapture(l);
        }
    }
}