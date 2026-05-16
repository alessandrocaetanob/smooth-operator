using BenchmarkDotNet.Running;

// Discovers every [Benchmark]-annotated class in this assembly.
// Pass --filter to run a subset, e.g. `-- --filter *SearchFiltering*`.
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
