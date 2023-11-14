namespace Sqids;

public class AhoCorasick
{
	private Trie Trie = new();

	public AhoCorasick(IEnumerable<string> words)
	{
		foreach (var word in words)
		{
			Trie.Add(word.AsSpan());
		}

		BuildFail();
	}

	public void BuildFail(Trie? node = null)
	{
		node ??= Trie;

		var word = node.Word;
		for (int i = 1; i < word.Length && node.Fail == null; i++)
			node.Fail = Trie.ExploreFailLink(word.AsSpan()[i..]);

		foreach (var subNode in node.Next)
			if (subNode != null)
				BuildFail(subNode);
	}

	public bool Search(ReadOnlySpan<char> text)
	{
		var current = Trie;

		for (int i = 0; i < text.Length; i++)
		{
			var c = text[i];

			while (current != null && current.Next[Trie.GetBucket(c)] == null)
				current = current.Fail;

			current ??= Trie;

			current = current.Next[Trie.GetBucket(c)];
			if (current != null)
			{
				var node = current;

				while (node != null)
				{
					if (node.IsWord)
					{
						return true;
					}

					node = node.Fail;
				}
			}
		}

		return false;
	}
}
