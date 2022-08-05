﻿// <copyright file="InterleavingHeuristic.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace ZenLib.ModelChecking
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using ZenLib.Generation;

    /// <summary>
    /// Class to conservatively estimate which variables
    /// must be interleaved to avoid exponential blowup in the BDD encoding.
    /// </summary>
    internal sealed class InterleavingHeuristic : IZenExprVisitor<Dictionary<long, object>, InterleavingResult>
    {
        /// <summary>
        /// Set of disjoint variable sets to track must-interleave dependencies, implemented as a union find.
        /// </summary>
        public UnionFind<object> DisjointSets { get; } = new UnionFind<object>();

        /// <summary>
        /// Expression cache to avoid redundant work.
        /// </summary>
        private Dictionary<object, InterleavingResult> cache = new Dictionary<object, InterleavingResult>();

        /// <summary>
        /// Computes the variable ordering requirements for the expression.
        /// </summary>
        /// <param name="expr">The Zen expression.</param>
        /// <param name="arguments">The argument to expression mapping.</param>
        /// <returns></returns>
        public List<List<object>> Compute<T>(Zen<T> expr, Dictionary<long, object> arguments)
        {
            var _ = Evaluate(expr, arguments);
            return this.DisjointSets.GetDisjointSets();
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Evaluate<T>(Zen<T> expression, Dictionary<long, object> parameter)
        {
            if (this.cache.TryGetValue(expression, out var value))
            {
                return value;
            }

            var result = expression.Accept(this, parameter);
            this.cache[expression] = result;
            return result;
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit(ZenLogicalBinopExpr expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.Expr1, parameter);
            var y = Evaluate(expression.Expr2, parameter);
            return x.Union(y);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit(ZenNotExpr expression, Dictionary<long, object> parameter)
        {
            return Evaluate(expression.Expr, parameter);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenIfExpr<T> expression, Dictionary<long, object> parameter)
        {
            Evaluate(expression.GuardExpr, parameter);
            var t = Evaluate(expression.TrueExpr, parameter);
            var f = Evaluate(expression.FalseExpr, parameter);
            return t.Union(f);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenConstantExpr<T> expression, Dictionary<long, object> parameter)
        {
            var emptySetGenerator = new InterleavingSetEmptyVisitor();
            return ReflectionUtilities.ApplyTypeVisitor(emptySetGenerator, typeof(T), Unit.Instance);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenArithBinopExpr<T> expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.Expr1, parameter);
            var y = Evaluate(expression.Expr2, parameter);
            this.Combine(x, y);
            return x.Union(y);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenBitwiseBinopExpr<T> expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.Expr1, parameter);
            var y = Evaluate(expression.Expr2, parameter);

            switch (expression.Operation)
            {
                case BitwiseOp.BitwiseAnd:
                case BitwiseOp.BitwiseXor:
                    this.Combine(x, y);
                    return x.Union(y);
                default:
                    Contract.Assert(expression.Operation == BitwiseOp.BitwiseOr);
                    return x.Union(y);
            }
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenBitwiseNotExpr<T> expression, Dictionary<long, object> parameter)
        {
            return Evaluate(expression.Expr, parameter);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenListEmptyExpr<T> expression, Dictionary<long, object> parameter)
        {
            var emptySetGenerator = new InterleavingSetEmptyVisitor();
            return ReflectionUtilities.ApplyTypeVisitor(emptySetGenerator, typeof(T), Unit.Instance);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenListAddFrontExpr<T> expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.ElementExpr, parameter);
            var y = Evaluate(expression.Expr, parameter);
            return x.Union(y);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<TKey, TValue>(ZenCastExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            return Evaluate(expression.SourceExpr, parameter);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<TList, TResult>(ZenListCaseExpr<TList, TResult> expression, Dictionary<long, object> parameter)
        {
            var _ = Evaluate(expression.ListExpr, parameter);
            var e = Evaluate(expression.EmptyExpr, parameter);
            return e; // no easy way to evaluate cons case.
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T1, T2>(ZenGetFieldExpr<T1, T2> expression, Dictionary<long, object> parameter)
        {
            var result = (InterleavingClass)Evaluate(expression.Expr, parameter);
            return result.Fields[expression.FieldName];
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T1, T2>(ZenWithFieldExpr<T1, T2> expression, Dictionary<long, object> parameter)
        {
            var x = (InterleavingClass)Evaluate(expression.Expr, parameter);
            var y = Evaluate(expression.FieldExpr, parameter);

            var result = new InterleavingClass(x.Fields.SetItem(expression.FieldName, y));
            this.cache[expression] = result;
            return result;
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<TObject>(ZenCreateObjectExpr<TObject> expression, Dictionary<long, object> parameter)
        {
            var fieldMapping = ImmutableDictionary<string, InterleavingResult>.Empty;
            foreach (var fieldValuePair in expression.Fields)
            {
                InterleavingResult valueResult;
                try
                {
                    var valueType = fieldValuePair.Value.GetType();
                    var innerType = valueType.BaseType.GetGenericArgumentsCached()[0];
                    var evaluateMethod = typeof(InterleavingHeuristic)
                        .GetMethodCached("Evaluate")
                        .MakeGenericMethod(innerType);
                    valueResult = (InterleavingResult)evaluateMethod.Invoke(this, new object[] { fieldValuePair.Value, parameter });
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }

                fieldMapping = fieldMapping.Add(fieldValuePair.Key, valueResult);
            }

            return new InterleavingClass(fieldMapping);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenEqualityExpr<T> expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.Expr1, parameter);
            var y = Evaluate(expression.Expr2, parameter);
            this.Combine(x, y);
            return x.Union(y);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenArithComparisonExpr<T> expression, Dictionary<long, object> parameter)
        {
            var x = Evaluate(expression.Expr1, parameter);
            var y = Evaluate(expression.Expr2, parameter);
            this.Combine(x, y);
            return x.Union(y);
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenArgumentExpr<T> expression, Dictionary<long, object> parameter)
        {
            try
            {
                var expr = parameter[expression.ArgumentId];
                var type = expr.GetType().BaseType.GetGenericArgumentsCached()[0];
                var evaluateMethod = typeof(InterleavingHeuristic)
                    .GetMethodCached("Evaluate")
                    .MakeGenericMethod(type);
                return (InterleavingResult)evaluateMethod.Invoke(this, new object[] { expr, parameter });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        public InterleavingResult Visit<T>(ZenArbitraryExpr<T> expression, Dictionary<long, object> parameter)
        {
            this.DisjointSets.Add(expression);
            var emptySetGenerator = new InterleavingSetEmptyVisitor();
            var emptySet = (InterleavingSet)ReflectionUtilities.ApplyTypeVisitor(emptySetGenerator, typeof(T), Unit.Instance);
            var variableSet = emptySet.GetAllVariables().Add(expression);
            return new InterleavingSet(variableSet);
        }

        /// <summary>
        /// Determines if a set of variables is comprised only of boolean values, which do not need interleaving.
        /// </summary>
        /// <param name="variableSet">The set of variables.</param>
        /// <returns>True or false.</returns>
        private bool IsBoolVariableSet(ImmutableHashSet<object> variableSet)
        {
            return variableSet.All(x => typeof(Zen<bool>).IsAssignableFrom(x.GetType()));
        }

        /// <summary>
        /// Combines two results to indicate that they depend on each other.
        /// Should replace this with a proper union-find data structure later.
        /// </summary>
        /// <param name="result1">The first result.</param>
        /// <param name="result2">The second result.</param>
        private void Combine(InterleavingResult result1, InterleavingResult result2)
        {
            var variableSet1 = result1.GetAllVariables();
            var variableSet2 = result2.GetAllVariables();

            if (IsBoolVariableSet(variableSet1) || IsBoolVariableSet(variableSet2))
            {
                return;
            }

            foreach (var variable1 in variableSet1)
            {
                foreach (var variable2 in variableSet2)
                {
                    var type1 = variable1.GetType().GetGenericArgumentsCached()[0];
                    var type2 = variable2.GetType().GetGenericArgumentsCached()[0];

                    if (type1 == type2)
                    {
                        this.DisjointSets.Union(variable1, variable2);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenMapEmptyExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenMapSetExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenMapGetExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenMapDeleteExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey>(ZenMapCombineExpr<TKey> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenConstMapSetExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<TKey, TValue>(ZenConstMapGetExpr<TKey, TValue> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid map type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqEmptyExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqConcatExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqUnitExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqLengthExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqAtExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqContainsExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqIndexOfExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqSliceExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqReplaceFirstExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }

        /// <summary>
        /// Evaluate an expression.
        /// </summary>
        /// <param name="expression">The zen expression.</param>
        /// <param name="parameter">The parameter.</param>
        /// <returns>The interleaving result.</returns>
        [ExcludeFromCodeCoverage]
        public InterleavingResult Visit<T>(ZenSeqRegexExpr<T> expression, Dictionary<long, object> parameter)
        {
            throw new ZenException($"Invalid sequence type used with Decision Diagram backend.");
        }
    }
}
