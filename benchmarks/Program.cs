using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Snail.MCP.Memory.Benchmarks.FormatterBenchmarks).Assembly).Run(args);
