﻿using System;
using System.Collections.Generic;

namespace Grimoire
{
    internal class EbnfOrExpression : EbnfBinaryExpression, IEquatable<EbnfOrExpression>, ICloneable
    {
        public EbnfOrExpression(EbnfExpression left, params EbnfExpression[] right)
        {
            if (null == right) right = new EbnfExpression[] { null };
            Left = left;
            for (var i = 0; i < right.Length; ++i)
                if (Right == null)
                    Right = right[i];
                else
                    Right = new EbnfOrExpression(Right, right[i]);
        }

        public EbnfOrExpression()
        {
        }

        public override bool IsTerminal => false;

        object ICloneable.Clone()
        {
            return Clone();
        }

        public bool Equals(EbnfOrExpression rhs)
        {
            if (ReferenceEquals(rhs, this)) return true;
            if (ReferenceEquals(rhs, null)) return false;
            return (Equals(Left, rhs.Left) && Equals(Right, rhs.Right)) ||
                   (Equals(Left, rhs.Right) && Equals(Right, rhs.Left));
        }

        public override IList<IList<string>> ToDisjunctions(EbnfDocument parent, Cfg cfg)
        {
            var l = new List<IList<string>>();
            if (null == Left)
                l.Add(new List<string>());
            else
                foreach (var ll in Left.ToDisjunctions(parent, cfg))
                    if (!l.Contains(ll, OrderedCollectionEqualityComparer<string>.Default))
                        l.Add(ll);
            if (null == Right)
            {
                var ll = new List<string>();
                if (!l.Contains(ll, OrderedCollectionEqualityComparer<string>.Default))
                    l.Add(ll);
            }
            else
            {
                foreach (var ll in Right.ToDisjunctions(parent, cfg))
                    if (!l.Contains(ll, OrderedCollectionEqualityComparer<string>.Default))
                        l.Add(ll);
            }

            return l;
        }

        public EbnfOrExpression Clone()
        {
            var result = new EbnfOrExpression(null != Left ? ((ICloneable)Left).Clone() as EbnfExpression : null,
                null != Right ? ((ICloneable)Right).Clone() as EbnfExpression : null);
            result.SetPositionInfo(Line, Column, Position);
            return result;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EbnfOrExpression);
        }

        public override int GetHashCode()
        {
            var result = 0;
            if (null != Left) result = Left.GetHashCode();
            if (null != Right) result ^= Right.GetHashCode();
            return result;
        }

        public static bool operator ==(EbnfOrExpression lhs, EbnfOrExpression rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return false;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(EbnfOrExpression lhs, EbnfOrExpression rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return false;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null)) return true;
            return !lhs.Equals(rhs);
        }

        public override string ToString()
        {
            return string.Concat(Left, " | ", Right);
        }
    }
}