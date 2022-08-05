﻿// <copyright file="Z3TypeToSortConverter.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace ZenLib.Solver
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.Z3;

    /// <summary>
    /// Convert a C# type into a Z3 sort.
    /// </summary>
    internal class Z3TypeToSortConverter : ITypeVisitor<Sort, Unit>
    {
        private SolverZ3 solver;

        private Dictionary<Type, Sort> typeToSort;

        public ISet<string> ObjectAppNames;

        public Z3TypeToSortConverter(SolverZ3 solver)
        {
            this.solver = solver;
            this.ObjectAppNames = new HashSet<string>();
            this.typeToSort = new Dictionary<Type, Sort>();
        }

        public Sort GetSortForType(Type type)
        {
            if (this.typeToSort.TryGetValue(type, out var sort))
            {
                return sort;
            }

            Sort result;
            if (type == ReflectionUtilities.SetUnitType)
            {
                result = this.solver.BoolSort;
            }
            else
            {
                result = ReflectionUtilities.ApplyTypeVisitor(this, type, Unit.Instance);
            }

            this.typeToSort[type] = result;
            return result;
        }

        public Sort VisitBigInteger(Unit parameter)
        {
            return this.solver.BigIntSort;
        }

        public Sort VisitReal(Unit parameter)
        {
            return this.solver.RealSort;
        }

        public Sort VisitBool(Unit parameter)
        {
            return this.solver.BoolSort;
        }

        public Sort VisitByte(Unit parameter)
        {
            return this.solver.ByteSort;
        }

        public Sort VisitChar(Unit parameter)
        {
            return this.solver.CharSort;
        }

        public Sort VisitMap(Type mapType, Type keyType, Type valueType, Unit parameter)
        {
            var keySort = this.GetSortForType(keyType);
            var valueSort = this.GetSortForType(valueType);

            if (valueType != ReflectionUtilities.SetUnitType)
            {
                valueSort = this.solver.GetOrCreateOptionSort(valueSort);
            }

            return SolverZ3.Context.MkArraySort(keySort, valueSort);
        }

        [ExcludeFromCodeCoverage]
        public Sort VisitConstMap(Type mapType, Type keyType, Type valueType, Unit parameter)
        {
            throw new ZenException("Can not use a const map in another map.");
        }

        public Sort VisitFixedInteger(Type intType, Unit parameter)
        {
            int size = ((dynamic)Activator.CreateInstance(intType, 0L)).Size;
            return SolverZ3.Context.MkBitVecSort((uint)size);
        }

        public Sort VisitInt(Unit parameter)
        {
            return this.solver.IntSort;
        }

        [ExcludeFromCodeCoverage]
        public Sort VisitList(Type listType, Type innerType, Unit parameter)
        {
            throw new ZenException("Can not use finite sequence type in another map.");
        }

        public Sort VisitLong(Unit parameter)
        {
            return this.solver.LongSort;
        }

        public Sort VisitObject(Type objectType, SortedDictionary<string, Type> objectFields, Unit parameter)
        {
            var fields = objectFields.ToArray();
            var fieldNames = new string[fields.Length];
            var fieldSorts = new Sort[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                fieldNames[i] = fields[i].Key;
                fieldSorts[i] = this.GetSortForType(fields[i].Value);
            }

            this.ObjectAppNames.Add(objectType.ToString());
            var objectConstructor = SolverZ3.Context.MkConstructor(objectType.ToString(), "value", fieldNames, fieldSorts);
            return SolverZ3.Context.MkDatatypeSort(objectType.ToString(), new Constructor[] { objectConstructor });
        }

        public Sort VisitShort(Unit parameter)
        {
            return this.solver.ShortSort;
        }

        public Sort VisitString(Unit parameter)
        {
            return this.solver.StringSort;
        }

        public Sort VisitUint(Unit parameter)
        {
            return this.solver.IntSort;
        }

        public Sort VisitUlong(Unit parameter)
        {
            return this.solver.LongSort;
        }

        public Sort VisitUshort(Unit parameter)
        {
            return this.solver.ShortSort;
        }

        public Sort VisitSeq(Type sequenceType, Type innerType, Unit parameter)
        {
            var valueSort = this.GetSortForType(innerType);
            return SolverZ3.Context.MkSeqSort(valueSort);
        }
    }
}
