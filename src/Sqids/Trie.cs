using System.Runtime.CompilerServices;

namespace Sqids;

public class Trie
{
	private static readonly int[] Index = {
	27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
	27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27, 27,
	0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
	27, 27, 27, 27, 27, 27, 27,
	4, 8, 10, 11, 3, 12, 6, 13, 1, 14, 15, 1, 16, 17, 0, 18, 19, 20, 5, 7, 21, 22, 23, 24, 25, 26,
	27, 27, 27, 27, 27, 27,
	4, 8, 10, 11, 3, 12, 6, 13, 1, 14, 15, 1, 16, 17, 0, 18, 19, 20, 5, 7, 21, 22, 23, 24, 25, 26,
	27, 27, 27, 27, 27
	};

	public Trie?[] Next = new Trie[28];
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
	}
}
