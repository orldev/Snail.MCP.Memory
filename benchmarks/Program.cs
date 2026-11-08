using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Snail.MCP.Memory.Benchmarks.AnswerBenchmarks).Assembly).Run(args);
