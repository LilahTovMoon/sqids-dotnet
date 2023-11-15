using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Sqids.Tests;

public class Benchmark
{
#if NET7_0_OR_GREATER
    private readonly SqidsEncoder<int> _sqidsEncoder = new();
#else
	private readonly SqidsEncoder _sqidsEncoder = new();
#endif

	// [Benchmark]
	// public string Encode() => _sqidsEncoder.Encode(int.MaxValue);

	// [Benchmark]
	// public bool IsBlocked() => _sqidsEncoder.IsBlockedId("zbO7j7");

	[Benchmark]
	public int Decode() => _sqidsEncoder.DecodeOne("zbO7j7");

	public class Program
	{
		public static void Main(string[] args)
		{
			var summary = BenchmarkRunner.Run<Benchmark>();
		}
	}
}
