namespace Sqids;

public class Program
{
	public static void Main()
	{
#if NET7_0_OR_GREATER
		var sqid = new SqidsEncoder<int>(new SqidsOptions
		{
			Alphabet = "0123456789",
		});
		Console.WriteLine(sqid.Encode(0));
#else
		var sqid = new SqidsEncoder(new SqidsOptions
		{
			Alphabet = "0123456789",
		});
		Console.WriteLine(sqid.Encode(0));
#endif
	}
}
