﻿// <copyright file="Program.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace ZenLibBench
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using ZenLib;

    /// <summary>
    /// Run a collection of benchmarks for Zen.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            ZenSettings.UseLargeStack = true;

            var x = Zen.Symbolic<int>();
            var m1 = Zen.Symbolic<ConstMap<string, int>>();
            var m2 = Zen.Symbolic<ConstMap<string, int>>();

            var c1 = m1.Get("a") == Zen.If(x < 10, x + 1, x + 2);
            var c2 = m2 == m1.Set("b", x);
            var solution = Zen.And(c1, c2).Solve();
            Console.WriteLine(solution.Get(x));
            Console.WriteLine(solution.Get(m1));
            Console.WriteLine(solution.Get(m2));

            // BenchmarkSets();
            // BenchmarkComparisons();
            // BenchmarkTransformers();
            // BenchmarkTransformerCache();
            // BenchmarkAllocation();
        }

        private static void BenchmarkSets()
        {
            Benchmark("BenchmarkSets", 3, () =>
            {
                var s = Zen.Symbolic<Set<string>>();

                for (int i = 0; i < 50; i++)
                {
                    s = s.Add(i.ToString());
                }

                for (int i = 0; i < 50; i++)
                {
                    s = s.Delete(i.ToString());
                }

                (s == Set.Empty<string>()).Solve();
            });

            Benchmark("BenchmarkConstSets", 10, () =>
            {
                var s = Zen.Symbolic<ConstSet<string>>();

                for (int i = 0; i < 50; i++)
                {
                    s = s.Add(i.ToString());
                }

                for (int i = 0; i < 50; i++)
                {
                    s = s.Delete(i.ToString());
                }

                var acc = Zen.False();
                for (int i = 0; i < 50; i++)
                {
                    acc = Zen.Or(acc, s.Contains(i.ToString()));
                }

                acc.Solve();
            });
        }

        private static void BenchmarkComparisons()
        {
            Benchmark(nameof(BenchmarkAllocation), 3, () =>
            {
                var values = new Zen<BigInteger>[1000];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = Zen.Symbolic<BigInteger>(i.ToString());
                }

                var min = values.Aggregate((a, b) => Zen.If(a < b, a, b));
                var solution = (min >= new BigInteger(100)).Solve();
            });
        }

        private static void BenchmarkTransformers()
        {
            Benchmark(nameof(BenchmarkAllocation), 1, () =>
            {
                var f = new Func<Zen<TestObject>, Zen<Option<TestObject>>>(p =>
                {
                    return Zen.If(
                        p.GetField<TestObject, uint>("DstIp") == 1,
                        Option.Create(p.WithField<TestObject, uint>("DstIp", 2)),
                        Zen.If(
                            p.GetField<TestObject, uint>("DstIp") == 3,
                            Option.Create(p.WithField<TestObject, uint>("DstIp", 4)),
                            Option.Create(p)
                            ));
                });

                var zf1 = new ZenFunction<TestObject, bool>(p => f(p).IsSome());
                var zf2 = new ZenFunction<TestObject, TestObject>(p => f(p).Value().Unroll());
                var full = new ZenFunction<TestObject, bool>(x => true).StateSet();

                var rnd = new Random(0);
                var randoms = new HashSet<(uint, uint)>();
                for (int i = 0; i < 1000; i++)
                {
                    randoms.Add(((uint)rnd.Next(0, 65000), (uint)rnd.Next(0, 65000)));
                }

                var largeSet = new ZenFunction<TestObject, bool>(p =>
                {
                    var constraints = randoms.Select(x => Zen.And(p.GetField<TestObject, uint>("DstIp") == x.Item1, p.GetField<TestObject, uint>("SrcIp") == x.Item2));
                    return Zen.Or(constraints.ToArray());
                }).StateSet();

                var t = zf2.Transformer();

                t.TransformForward(largeSet);
                t.TransformBackwards(largeSet);
            });
        }

        private static void BenchmarkTransformerCache()
        {
            Benchmark(nameof(BenchmarkAllocation), 20000, () =>
            {
                var zf = new ZenFunction<uint, bool>(x => Zen.And(x <= 90, x >= 30));
                zf.StateSet();
                zf.Transformer();
            });
        }

        private static void BenchmarkAllocation()
        {
            Benchmark(nameof(BenchmarkAllocation), 30, () =>
            {
                var b = new AclBench();
                b.Backend = ZenLib.ModelChecking.Backend.DecisionDiagrams;
                b.NumLines = 10000;
                b.CreateAcl();
                b.Acl.ProcessProvenance(Zen.Arbitrary<ZenLib.Tests.Network.IpHeader>());
            });
        }

        private static void Benchmark(string name, int iterations, Action action)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                action();
            }

            timer.Stop();
            Console.WriteLine($"[{name}]: Total: {timer.ElapsedMilliseconds}ms, Avg {timer.ElapsedMilliseconds / iterations}ms");
        }
    }

    /// <summary>
    ///     A test class.
    /// </summary>
    public class TestObject
    {
        /// <summary>
        ///     Gets or sets the destination IP address.
        /// </summary>
        public uint DstIp { get; set; }

        /// <summary>
        ///     Gets or sets the source IP address.
        /// </summary>
        public uint SrcIp { get; set; }

        /// <summary>
        /// Foo field.
        /// </summary>
        public uint Foo { get; set; }

        /// <summary>
        /// Bar field.
        /// </summary>
        public uint Bar { get; set; }

        /// <summary>
        /// Baz field.
        /// </summary>
        public uint Baz { get; set; }
    }
}
