using System.Buffers;
using System.Runtime.InteropServices;
#if NET7_0_OR_GREATER
using System.Numerics;
#endif

namespace Sqids;

#if NET7_0_OR_GREATER
/// <summary>
/// The Sqids encoder/decoder. This is the main class.
/// </summary>
/// <typeparam name="T">
/// The integral numeric type that will be encoded/decoded.
/// Could be one of `int`, `long`, `byte`, `short`, and others. For the full list, check out
/// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types
/// </typeparam>
public sealed class SqidsEncoder<T> where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
#else
/// <summary>
/// The Sqids encoder/decoder. This is the main class.
/// </summary>
public sealed class SqidsEncoder
#endif
{
	private const int MinAlphabetLength = 3;
	private const int MaxMinLength = 255;

	private readonly char[] _alphabet;
	private readonly int _minLength;
	private readonly int _maxLength;
	private readonly AhoCorasick _ahoCorasick;

#if NET7_0_OR_GREATER
	private readonly T _alphabetLength;
#endif

#if NET7_0_OR_GREATER
	/// <summary>
	/// Initializes a new instance of <see cref="SqidsEncoder{T}" /> with the default options.
	/// </summary>
#else
	/// <summary>
	/// Initializes a new instance of <see cref="SqidsEncoder" /> with the default options.
	/// </summary>
#endif
	public SqidsEncoder() : this(new()) { }

#if NET7_0_OR_GREATER
	/// <summary>
	/// Initializes a new instance of <see cref="SqidsEncoder{T}" /> with custom options.
	/// </summary>
	/// <param name="options">
	/// The custom options.
	/// All properties of <see cref="SqidsOptions" /> are optional and will fall back to their
	/// defaults if not explicitly set.
	/// </param>
	/// <exception cref="T:System.ArgumentNullException" />
	/// <exception cref="T:System.ArgumentOutOfRangeException" />
#else
	/// <summary>
	/// Initializes a new instance of <see cref="SqidsEncoder" /> with custom options.
	/// </summary>
	/// <param name="options">
	/// The custom options.
	/// All properties of <see cref="SqidsOptions" /> are optional and will fall back to their
	/// defaults if not explicitly set.
	/// </param>
	/// <exception cref="T:System.ArgumentNullException" />
	/// <exception cref="T:System.ArgumentOutOfRangeException" />
#endif
	public SqidsEncoder(SqidsOptions options)
	{
		_ = options ?? throw new ArgumentNullException(nameof(options));
		_ = options.Alphabet ?? throw new ArgumentNullException(nameof(options.Alphabet));
		_ = options.BlockList ?? throw new ArgumentNullException(nameof(options.BlockList));

		if (options.Alphabet.Distinct().Count() != options.Alphabet.Length)
			throw new ArgumentOutOfRangeException(
				nameof(options.MinLength),
				"The alphabet must not contain duplicate characters."
			);

		if (Encoding.UTF8.GetByteCount(options.Alphabet) != options.Alphabet.Length)
			throw new ArgumentOutOfRangeException(
				nameof(options.MinLength),
				"The alphabet must not contain multi-byte characters."
			);

		if (options.Alphabet.Length < MinAlphabetLength)
			throw new ArgumentOutOfRangeException(
				nameof(options.Alphabet),
				$"The alphabet must contain at least {MinAlphabetLength} characters."
			);

		if (options.MinLength < 0 || options.MinLength > MaxMinLength)
			throw new ArgumentOutOfRangeException(
				nameof(options.MinLength),
				$"The minimum length must be between 0 and {MaxMinLength}."
			);

		_minLength = options.MinLength;

		// NOTE: Cleanup the blocklist:
		options.BlockList = new HashSet<string>(
			options.BlockList,
			StringComparer.OrdinalIgnoreCase // NOTE: Effectively removes items that differ only in casing — leaves one version of each word casing-wise which will then be compared against the generated IDs case-insensitively
		);
		options.BlockList.RemoveWhere(w =>
			// NOTE: Removes words that are less than 3 characters long
			w.Length < 3 ||
			// NOTE: Removes words that contain characters not found in the alphabet
#if NETSTANDARD2_0
			w.Any(c => options.Alphabet.IndexOf(c.ToString(), StringComparison.OrdinalIgnoreCase) == -1) // NOTE: A `string.Contains` overload with `StringComparison` didn't exist prior to .NET Standard 2.1, so we have to resort to `IndexOf` — see https://stackoverflow.com/a/52791476
#else
			w.Any(c => !options.Alphabet.Contains(c, StringComparison.OrdinalIgnoreCase))
#endif
		);

		_ahoCorasick = new AhoCorasick(options.BlockList);

		_alphabet = options.Alphabet.ToArray();
#if NET7_0_OR_GREATER
		_maxLength = (int) Math.Ceiling(Math.Log(double.CreateChecked(T.MaxValue)) / Math.Log(_alphabet.Length - 2) + 1);
		_alphabetLength = T.CreateChecked(_alphabet.Length);
#else
		_maxLength = (int) Math.Ceiling(Math.Log(int.MaxValue) / Math.Log(_alphabet.Length - 2) + 1);
#endif
		ConsistentShuffle(_alphabet);
	}

	/// <summary>
	/// Encodes a single number into a Sqids ID.
	/// </summary>
	/// <param name="number">The number to encode.</param>
	/// <returns>A string containing the encoded ID.</returns>
	/// <exception cref="T:System.ArgumentOutOfRangeException">If the number passed is smaller than 0 (i.e. negative).</exception>
	/// <exception cref="T:System.ArgumentException">If the encoding reaches maximum re-generation attempts due to the blocklist.</exception>
#if NET7_0_OR_GREATER
	public string Encode(T number, int increment = 0)
#else
	public string Encode(int number, int increment = 0)
#endif
	{
#if NET7_0_OR_GREATER
		if (number < T.Zero)
#else
		if (number < 0)
#endif
			throw new ArgumentOutOfRangeException(
				nameof(number),
				"Encoding is only supported for zero and positive numbers."
			);

		if (increment > _alphabet.Length)
			throw new ArgumentException("Reached max attempts to re-generate the ID.");

#if NET7_0_OR_GREATER
		int offset = _alphabet[int.CreateChecked(number % _alphabetLength)];
#else
        int offset = _alphabet[number % _alphabet.Length];
#endif
		offset = (1 + offset) % _alphabet.Length;
		offset = (offset + increment) % _alphabet.Length;

		Span<char> alphabetTemp = stackalloc char[_alphabet.Length];
		var alphabetSpan = _alphabet.AsSpan();
		alphabetSpan[offset..].CopyTo(alphabetTemp);
		alphabetSpan[..offset].CopyTo(alphabetTemp[^offset..]);

		Span<char> builder = stackalloc char[_maxLength]; // TODO: pool a la Hashids.net?
		int builderIndex = 1;
		builder[0] = alphabetTemp[0];
		alphabetTemp.Reverse();


#if NET7_0_OR_GREATER
		ToId(number, alphabetTemp[1..], builder, ref builderIndex, _alphabetLength);
#else
		ToId(number, alphabetTemp[1..], builder, ref builderIndex);
#endif

		if (builderIndex < _minLength)
		{
			builder[builderIndex++] = alphabetTemp[0];

			if (builderIndex < _minLength)
			{
				ConsistentShuffle(alphabetTemp);
				int toIndex = Math.Min(_minLength - builderIndex, _alphabet.Length);
				alphabetTemp[..toIndex].CopyTo(builder[builderIndex..]);
				builderIndex += toIndex;
			}
		}

		if (IsBlockedId(builder[..builderIndex]))
			return Encode(number, increment + 1);

#if NET7_0_OR_GREATER
		return new string(builder[..builderIndex]);
#else
		return new string(builder[..builderIndex].ToArray());
#endif
	}

	/// <summary>
	/// Encodes multiple numbers into a Sqids ID.
	/// </summary>
	/// <param name="numbers">The numbers to encode.</param>
	/// <returns>A string containing the encoded IDs, or an empty string if the array passed is empty.</returns>
	/// <exception cref="T:System.ArgumentOutOfRangeException">If any of the numbers passed is smaller than 0 (i.e. negative).</exception>
	/// <exception cref="T:System.ArgumentException">If the encoding reaches maximum re-generation attempts due to the blocklist.</exception>
#if NET7_0_OR_GREATER
	public string Encode(params T[] numbers)
#else
	public string Encode(params int[] numbers)
#endif
	{
		if (numbers.Length == 0)
			return string.Empty;

		foreach (var number in numbers)
#if NET7_0_OR_GREATER
			if (number < T.Zero)
#else
			if (number < 0)
#endif
				throw new ArgumentOutOfRangeException(
					nameof(numbers),
					"Encoding is only supported for zero and positive numbers."
				);

		return Encode(numbers.AsSpan());
	}

	/// <summary>
	/// Encodes a collection of numbers into a Sqids ID.
	/// </summary>
	/// <param name="numbers">The numbers to encode.</param>
	/// <returns>A string containing the encoded IDs, or an empty string if the `IEnumerable` passed is empty.</returns>
	/// <exception cref="T:System.ArgumentOutOfRangeException">If any of the numbers passed is smaller than 0 (i.e. negative).</exception>
	/// <exception cref="T:System.ArgumentException">If the encoding reaches maximum re-generation attempts due to the blocklist.</exception>
#if NET7_0_OR_GREATER
	public string Encode(IEnumerable<T> numbers) =>
#else
	public string Encode(IEnumerable<int> numbers) =>
#endif
		Encode(numbers.ToArray());

	// TODO: Consider using `ArrayPool` if possible
#if NET7_0_OR_GREATER
	private string Encode(ReadOnlySpan<T> numbers, int increment = 0)
#else
	private string Encode(ReadOnlySpan<int> numbers, int increment = 0)
#endif
	{
		if (increment > _alphabet.Length)
			throw new ArgumentException("Reached max attempts to re-generate the ID.");

		int offset = 0;
		for (int i = 0; i < numbers.Length; i++)
#if NET7_0_OR_GREATER
			offset += _alphabet[int.CreateChecked(numbers[i] % _alphabetLength)] + i;
#else
			offset += _alphabet[numbers[i] % _alphabet.Length] + i;
#endif
		offset = (numbers.Length + offset) % _alphabet.Length;
		offset = (offset + increment) % _alphabet.Length;

		Span<char> alphabetTemp = stackalloc char[_alphabet.Length];
		var alphabetSpan = _alphabet.AsSpan();
		alphabetSpan[offset..].CopyTo(alphabetTemp[..^offset]);
		alphabetSpan[..offset].CopyTo(alphabetTemp[^offset..]);

		Span<char> builder = stackalloc char[_maxLength]; // TODO: pool a la Hashids.net?
		int builderIndex = 1;
		builder[0] = alphabetTemp[0];
		alphabetTemp.Reverse();

		for (int i = 0; i < numbers.Length; i++)
		{
			var number = numbers[i];
			var alphabetWithoutSeparator = alphabetTemp[1..]; // NOTE: Excludes the first character — which is the separator
#if NET7_0_OR_GREATER
            ToId(number, alphabetWithoutSeparator, builder, ref builderIndex, _alphabetLength);
#else
			ToId(number, alphabetWithoutSeparator, builder, ref builderIndex);
#endif

			if (i >= numbers.Length - 1) // NOTE: If the last one
				continue;

			builder[builderIndex++] = alphabetTemp[0];
			ConsistentShuffle(alphabetTemp);
		}

		if (builderIndex < _minLength)
		{
			builder[builderIndex++] = alphabetTemp[0];

			if (builderIndex < _minLength)
			{
				ConsistentShuffle(alphabetTemp);
				int toIndex = Math.Min(_minLength - builderIndex, _alphabet.Length);
				alphabetTemp[..toIndex].CopyTo(builder[builderIndex..]);
				builderIndex += toIndex;
			}
		}

		if (IsBlockedId(builder[..builderIndex]))
			return Encode(numbers, increment + 1);

#if NET7_0_OR_GREATER
		return new string(builder[..builderIndex]);
#else
		return new string(builder[..builderIndex].ToArray());
#endif
	}

#if NET7_0_OR_GREATER
	public T DecodeOne(ReadOnlySpan<char> id)
#else
	public int DecodeOne(ReadOnlySpan<char> id)
#endif
	{
		if (id.IsEmpty)
			return default;
		var alphabetSpan = _alphabet.AsSpan();

		char prefix = id[0];
		int offset = alphabetSpan.IndexOf(prefix);

		Span<char> alphabetTemp = stackalloc char[_alphabet.Length];
		alphabetSpan[offset..].CopyTo(alphabetTemp);
		alphabetSpan[..offset].CopyTo(alphabetTemp[^offset..]);

		alphabetTemp.Reverse();

		id = id[1..]; // NOTE: Exclude the prefix

		var separatorIndex = id.IndexOf(alphabetTemp[0]);
		id = separatorIndex == -1 ? id : id[..separatorIndex]; // NOTE: The first part of `id` (every thing to the left of the separator) represents the number that we ought to decode.

		if (id.IsEmpty)
			return default;

		return ToNumber(id, alphabetTemp[1..]);
	}


	/// <summary>
	/// Decodes an ID into numbers.
	/// </summary>
	/// <param name="id">The encoded ID.</param>
	/// <returns>
	/// An array containing the decoded number(s) (it would contain only one element
	/// if the ID represents a single number); or an empty array if the input ID is null,
	/// empty, or includes characters not found in the alphabet.
	/// </returns>
#if NET7_0_OR_GREATER
	public IReadOnlyList<T> Decode(ReadOnlySpan<char> id)
#else
	public IReadOnlyList<int> Decode(ReadOnlySpan<char> id)
#endif
	{
		if (id.IsEmpty)
#if NET7_0_OR_GREATER
			return Array.Empty<T>();
#else
			return Array.Empty<int>();
#endif

// 		foreach (char c in id)
// 			if (!_alphabet.Contains(c))
// #if NET7_0_OR_GREATER
// 				return Array.Empty<T>();
// #else
// 				return Array.Empty<int>();
// #endif

		var alphabetSpan = _alphabet.AsSpan();

		char prefix = id[0];
		int offset = alphabetSpan.IndexOf(prefix);

		Span<char> alphabetTemp = stackalloc char[_alphabet.Length];
		alphabetSpan[offset..].CopyTo(alphabetTemp);
		alphabetSpan[..offset].CopyTo(alphabetTemp[^offset..]);

		alphabetTemp.Reverse();

		id = id[1..]; // NOTE: Exclude the prefix

#if NET7_0_OR_GREATER
		var result = new List<T>(1);
#else
		var result = new List<int>(1);
#endif
		while (!id.IsEmpty)
		{
			char separator = alphabetTemp[0];

			var separatorIndex = id.IndexOf(separator);
			var chunk = separatorIndex == -1 ? id : id[..separatorIndex]; // NOTE: The first part of `id` (every thing to the left of the separator) represents the number that we ought to decode.
			id = separatorIndex == -1 ? default : id[(separatorIndex + 1)..]; // NOTE: Everything to the right of the separator will be `id` for the next iteration

			if (chunk.IsEmpty)
				return result;

			var decodedNumber = ToNumber(chunk, alphabetTemp[1..]);
			result.Add(decodedNumber);

			if (!id.IsEmpty)
				ConsistentShuffle(alphabetTemp);
		}

		return result;
	}

	// NOTE: Implicit `string` => `Span<char>` conversion was introduced in .NET Standard 2.1 (see https://learn.microsoft.com/en-us/dotnet/api/system.string.op_implicit), which means without this overload, calling `Decode` with a string on versions older than .NET Standard 2.1 would require calling `.AsSpan()` on the string, which is cringe.
#if NETSTANDARD2_0
	/// <summary>
	/// Decodes an ID into numbers.
	/// </summary>
	/// <param name="id">The encoded ID.</param>
	/// <returns>
	/// An array containing the decoded number(s) (it would contain only one element
	/// if the ID represents a single number); or an empty array if the input ID is null,
	/// empty, or includes characters not found in the alphabet.
	/// </returns>
	public IReadOnlyList<int> Decode(string id) => Decode(id.AsSpan());
#endif

	public bool IsBlockedId(ReadOnlySpan<char> id)
	{
		return _ahoCorasick.Search(id);
	}

	// NOTE: Shuffles a span of characters in place. The shuffle produces consistent results.
	private static void ConsistentShuffle(Span<char> chars)
	{
		for (int i = 0, j = chars.Length - 1; j > 0; i++, j--)
		{
			int r = (i * j + chars[i] + chars[j]) % chars.Length;
			(chars[i], chars[r]) = (chars[r], chars[i]);
		}
	}

#if NET7_0_OR_GREATER
	private static void ToId(T num, ReadOnlySpan<char> alphabet, Span<char> builder, ref int builderIndex, T alphabetLength)
#else
	private static void ToId(int num, ReadOnlySpan<char> alphabet, Span<char> builder, ref int builderIndex)
#endif
	{
		int start = builderIndex;
#if NET7_0_OR_GREATER
		do
		{
			builder[builderIndex++] = alphabet[int.CreateChecked(num % alphabetLength)];
			num /= alphabetLength;
		} while (num > T.Zero);
#else
		do
		{
			builder[builderIndex++] = alphabet[num % alphabet.Length];
			num /= alphabet.Length;
		} while (num > 0);
#endif
		builder[start..builderIndex].Reverse();
	}

#if NET7_0_OR_GREATER
	private static T ToNumber(ReadOnlySpan<char> id, ReadOnlySpan<char> alphabet)
#else
	private static int ToNumber(ReadOnlySpan<char> id, ReadOnlySpan<char> alphabet)
#endif
	{
#if NET7_0_OR_GREATER
		T result = T.Zero;
		T alphabetLength = T.CreateChecked(alphabet.Length);
		foreach (var character in id)
			result = result * alphabetLength + T.CreateChecked(alphabet.IndexOf(character)); //TODO Sho: create an array of ASCII to the value thingy
		//that probably wont be faster because it'd be different for every alphabet iteration and creating the array would take longer
		//though one could precompute the 62 different arrays and go with that?
		//no because they get shuffled.
#else
		int result = 0;
		foreach (var character in id)
			result = result * alphabet.Length + alphabet.IndexOf(character);
#endif
		return result;
	}


}
