using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(SolSharp.Benchmarks.SigningBenchmarks).Assembly).Run(args);
