namespace Sqids;

public class LeetComparator : EqualityComparer<char>
{
	public override bool Equals(char x, char y)
	{

		return Leetifier(x).Equals(Leetifier(y));
	}

	public override int GetHashCode(char obj)
	{
		return Leetifier(obj);
	}

	public static char Leetifier(char x)
	{
		x = char.ToLowerInvariant(x);
		return x switch
		{
			'o' => '0',
			'b' => '8',
			'e' => '3',
			'a' => '4',
			'g' => '6',
			'l' => '1',
			'i' => '1',
			's' => '5',
			't' => '7',
			_ => x
		};
	}
}
