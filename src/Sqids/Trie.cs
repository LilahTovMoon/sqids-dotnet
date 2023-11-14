namespace Sqids;

public class Trie
{
	public Trie?[] Next = new Trie[68];
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

	public static int GetBucket(char c)
	{
		if (c > 'z')
			return c - (char)58;
		if (c < 'A')
			return c - (char)32;
		if (c > '`')
			c -= (char)32;
		if (c > 'Z')
			return c - (char)32;
		c = c switch
		{
			'O' => '0',
			'B' => '8',
			'E' => '3',
			'A' => '4',
			'G' => '6',
			'L' => '1',
			'I' => '1',
			'S' => '5',
			'T' => '7',
			_ => c
		};
		return c - (char)32;
	}
}
