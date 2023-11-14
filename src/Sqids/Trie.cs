using System.Runtime.CompilerServices;

namespace Sqids;

public class Trie
{
	private static readonly int[] Index = {
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0, 0, 0, 0, 0, 0, 0, 11, 12, 13, 14, 15, 16,
		17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 0, 0, 0, 0, 0, 0, 11, 12, 13,
		14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 0, 0, 0, 0, 0
	};

	public Trie?[] Next = new Trie[37];
	public bool IsWord;
	public Trie? Fail;
	public string Word = string.Empty;


	public Trie Add(ReadOnlySpan<char> word)
	{
		var c = word[0];

		var node = Next[GetBucket(c)];
		if (node == null)
			Next[GetBucket(c)] = node = new Trie { Word = Word + c };

		if (word.Length > 1)
			return node.Add(word[1..]);

		node.IsWord = true;
		return node;
	}

	public Trie? ExploreFailLink(ReadOnlySpan<char> word)
	{
		var node = this;

		foreach (var c in word)
		{
			node = node.Next[GetBucket(c)];
			if (node == null) return null;
		}

		return node;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetBucket(char c)
	{
		return Index[c];
		// if (c < ':')
		// 	return c - (char)48;
		// if (c > '`')
		// 	c -= (char)32;
		// c = c switch
		// {
		// 	'O' => '0',
		// 	'B' => '8',
		// 	'E' => '3',
		// 	'A' => '4',
		// 	'G' => '6',
		// 	'L' => '1',
		// 	'I' => '1',
		// 	'S' => '5',
		// 	'T' => '7',
		// 	_ => c
		// };
		// if (c > ':')
		// 	return c - (char)55;
		// return c - (char)48;
	}
}
