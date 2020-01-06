using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Architect.Encodings
{
	/// <summary>
	/// <para>
	/// Transcodes between binary data and Base62 alphanumeric text.
	/// </para>
	/// <para>
	/// Every block of 8 bytes is represented as a block of 11 ASCII characters. Incomplete blocks are represented by as many characters as are necessary. There is no padding.
	/// </para>
	/// <para>
	/// The Base62 alphabet, representing the values 0 through 61 respectively, is 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.
	/// </para>
	/// <para>
	/// Changes to any byte in a sequence result in similar changes near the same relative position in the Base62 representation.
	/// The one caveat is that a change anywhere inside an 11-character Base62 sub-block may also alter the rest of that block's characters to the right.
	/// </para>
	/// </summary>
	public static class Base62Encoder
	{
		private static Base62Alphabet DefaultAlphabet { get; } = new Base62Alphabet(Encoding.ASCII.GetBytes("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"));

		/// <summary>
		/// A vector filled with the ushort value 127, the maximum ASCII byte value.
		/// </summary>
		private static Vector<ushort> VectorOf127s = new Vector<ushort>(127);

		static Base62Encoder()
		{
			if (!BitConverter.IsLittleEndian)
				throw new NotSupportedException("Big-endian architectures are not currently supported.");
		}

		/// <summary>
		/// Returns the number of Base62 characters required to represent the given number of bytes.
		/// </summary>
		public static int GetBase62Length(int byteLength)
		{
			// Avoid overflow
			const int MaxLength = (Int32.MaxValue - 7) / 11;
			if (byteLength < 0 || byteLength > MaxLength) throw new ArgumentOutOfRangeException(nameof(byteLength));

			// By adding 7 (i.e. 8 - 1), we get the ceiling of the division
			return (byteLength * 11 + 7) / 8;
		}
		
		/// <summary>
		/// Returns the number of bytes represented by the given number of Base62 characters.
		/// Throws if the given number is not a valid Base62 length.
		/// </summary>
		public static int GetByteLength(int base62Length)
		{
			// Avoid overflow
			const int MaxLength = Int32.MaxValue / 8;
			if (base62Length > MaxLength) throw new ArgumentOutOfRangeException(nameof(base62Length));

			var result = base62Length * 8 / 11;
			if (GetBase62Length(result) != base62Length) throw new ArgumentException("The input length was not a valid Base62 length.");
			return result;
		}

		/// <summary>
		/// Allocates a new string, encoding the given bytes as alphanumeric Base62 characters.
		/// </summary>
		public static string ToBase62String(byte[] bytes, Base62Alphabet alphabet = null)
		{
			return ToBase62String(bytes.AsSpan(), alphabet);
		}

		/// <summary>
		/// Allocates a new string, encoding a subset of the given bytes as alphanumeric Base62 characters.
		/// </summary>
		public static string ToBase62String(byte[] bytes, int offset, int length, Base62Alphabet alphabet = null)
		{
			return ToBase62String(bytes.AsSpan(offset, length), alphabet);
		}

		/// <summary>
		/// Allocates a new string, encoding the given bytes as alphanumeric Base62 characters.
		/// </summary>
		public static string ToBase62String(ReadOnlySpan<byte> bytes, Base62Alphabet alphabet = null)
		{
			// We need a temporary place to store bytes, since we get UTF8 but want a string
			// That is alright:
			// We cannot use String.Create with span input anyway, so we need to make a full copy somewhere - it might as well be here

			// As an alternative to filling a byte[] and using ASCII.GetString(), we could fill a char[] and use new string(chars)
			// Filling the chars could be slower, yet creating the string could be faster
			// The overall performance is similar, so we prefer our own simplest implementation

			// Prefer stackalloc over renting
			var outputLength = GetBase62Length(bytes.Length);
			byte[] charArray = null;
			var chars = outputLength <= 1024
				? stackalloc byte[outputLength]
				: (charArray = ArrayPool<byte>.Shared.Rent(outputLength));
			try
			{
				if (!TryToBase62Chars(bytes, chars, out _, alphabet))
					throw new ArgumentException("The output span is too short.");

				return Encoding.ASCII.GetString(chars);
			}
			finally
			{
				if (charArray != null) ArrayPool<byte>.Shared.Return(charArray);
			}
		}

		/// <summary>
		/// <para>
		/// Encodes the given bytes as alphanumeric Base62 characters, writing them to the output span as ASCII bytes.
		/// </para>
		/// <para>
		/// Returns false if the output span is too short.
		/// If false is returned, any number of characters may have been written, and the output value of the number of characters written has no meaning.
		/// </para>
		/// </summary>
		public static bool TryToBase62Chars(ReadOnlySpan<byte> bytes, Span<byte> chars, out int charsWritten, Base62Alphabet alphabet = null)
		{
			return TryToBase62CharsCore(bytes, chars, shiftCount: 0, out charsWritten, alphabet);
		}
		
		/// <summary>
		/// <para>
		/// Represents the given bytes as alphanumeric Base62 characters, writing them to the output span as UTF-16 characters.
		/// </para>
		/// <para>
		/// Returns false if the output span is too short.
		/// </para>
		/// </summary>
		public static bool TryToBase62Chars(ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten, Base62Alphabet alphabet = null)
		{
			var charsAsBytes = MemoryMarshal.AsBytes(chars);
			return TryToBase62CharsCore(bytes, charsAsBytes, shiftCount: 1, out charsWritten, alphabet);
		}

		/// <summary>
		/// Core implementation.
		/// Returns true on success or false if the output span was too short.
		/// </summary>
		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static bool TryToBase62CharsCore(ReadOnlySpan<byte> bytes, Span<byte> chars, int shiftCount, out int charsWritten, Base62Alphabet alphabet = null)
		{
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);

			var forwardAlphabet = (alphabet ?? DefaultAlphabet).ForwardAlphabet;

			charsWritten = 0;

			var outputLength = GetBase62Length(bytes.Length);
			if (chars.Length < outputLength << shiftCount) return false;
			chars = chars.Slice(0, outputLength << shiftCount);

			charsWritten = outputLength;

			// Encode complete blocks
			while (bytes.Length >= 8)
			{
				EncodeBlock(forwardAlphabet, bytes, chars, shiftCount);
				bytes = bytes.Slice(8);
				chars = chars.Slice(11 << shiftCount);
			}

			if (bytes.IsEmpty) return true;

			// Encode the final (incomplete) block
			{
				EncodeBlock(forwardAlphabet, bytes, chars, shiftCount);
				return true;
			}
		}

		/// <summary>
		/// Encodes the given block of up to 8 bytes into the corresponding number of UTF-8 of UTF-16 bytes (shift count 0 or 1 respectively).
		/// </summary>
		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static void EncodeBlock(ReadOnlySpan<byte> alphabet, ReadOnlySpan<byte> bytes, Span<byte> chars, int shiftCount)
		{
			System.Diagnostics.Debug.Assert(alphabet.Length == 62);
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);
			System.Diagnostics.Debug.Assert(bytes.Length >= 1);
			System.Diagnostics.Debug.Assert(chars.Length >= 2 << shiftCount);

			var ulongValue = 0UL;
			var max = Math.Min(8, bytes.Length);
			for (var i = 0; i < max; i++) ulongValue = (ulongValue << 8) | bytes[i];

			// Can encode 8 bytes as 11 chars
			var initial = Math.Min(11 << shiftCount, chars.Length) - (1 << shiftCount);
			var decrement = 1 << shiftCount;
			for (var i = initial; i >= 0; i -= decrement) // Chars are little-endian, so the left byte of each pair contains the value if operating on chars
			{
				var quotient = ulongValue / 62UL;
				var remainder = ulongValue - 62UL * quotient;
				ulongValue = quotient;
				chars[i] = alphabet[(int)remainder];
			}
		}

		/// <summary>
		/// Allocates a new byte array, containing the data represented by the given Base62 string.
		/// </summary>
		public static byte[] FromBase62String(string base62String, Base62Alphabet alphabet = null)
		{
			// Prefer stackalloc over renting
			byte[] charBytes = null;
			var chars = base62String.Length <= 1024
				? stackalloc byte[base62String.Length]
				: (charBytes = ArrayPool<byte>.Shared.Rent(base62String.Length));
			try
			{
				Encoding.ASCII.GetBytes(base62String, chars);
				var bytes = new byte[GetByteLength(chars.Length)];
				if (!TryFromBase62Chars(chars, bytes, out _, alphabet))
					throw new ArgumentException("The input is not valid Base62.");
				return bytes;
			}
			finally
			{
				if (charBytes != null) ArrayPool<byte>.Shared.Return(charBytes);
			}
		}

		/// <summary>
		/// Allocates a new byte array, decoding the data represented by the given Base62 string.
		/// </summary>
		public static bool TryFromBase62String(string base62String, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			return TryFromBase62Chars(base62String.AsSpan(), bytes, out bytesWritten, alphabet);
		}
		
		/// <summary>
		/// <para>
		/// Decodes the given ASCII-encoded Base62 characters back into the represented bytes, writing them to the output span.
		/// </para>
		/// <para>
		/// Returns false if the output span is too short or the input is not valid Base62.
		/// If false is returned, any number of bytes may have been written, and the output value of the number of bytes written has no meaning.
		/// </para>
		/// </summary>
		public static bool TryFromBase62Chars(ReadOnlySpan<byte> chars, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			return TryFromBase62CharsCore(chars, shiftCount: 0, bytes, out bytesWritten, alphabet);
		}

		/// <summary>
		/// <para>
		/// Decodes the given Base62 characters back into the represented bytes, writing them to the output span.
		/// </para>
		/// <para>
		/// Returns false if the output span is too short or the input is not valid Base62.
		/// If false is returned, any number of bytes may have been written, and the output value of the number of bytes written has no meaning.
		/// </para>
		/// </summary>
		public static bool TryFromBase62Chars(ReadOnlySpan<char> chars, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			bytesWritten = 0;

			// Since we will only be processing the lower byte of each char, make sure that no char has data in its higher byte
			// Otherwise, invalid input could get through
			{
				var vectors = MemoryMarshal.Cast<char, Vector<ushort>>(chars); // Takes only full vector blocks of the input span
				var hasNonAsciiByte = false;
				foreach (var vector in vectors) hasNonAsciiByte |= Vector.GreaterThanAny(vector, VectorOf127s); // Generally compares 16 chars at a time
				for (var i = vectors.Length * Vector<ushort>.Count; i < chars.Length; i++) hasNonAsciiByte |= chars[i] > 127; // Compare the remaining chars
				if (hasNonAsciiByte) return false;
			}

			var charsAsBytePairs = MemoryMarshal.AsBytes(chars);
			return TryFromBase62CharsCore(charsAsBytePairs, shiftCount: 1, bytes, out bytesWritten, alphabet);
		}

		/// <summary>
		/// Core implementation.
		/// Returns true on success, or false if the output span was too short or the input was not valid Base62.
		/// </summary>
		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static bool TryFromBase62CharsCore(ReadOnlySpan<byte> chars, int shiftCount, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);

			if (alphabet is null) alphabet = DefaultAlphabet;
			var reverseAlphabet = alphabet.ReverseAlphabet;
			
			try
			{
				var outputLength = GetByteLength(chars.Length >> shiftCount);
				if (bytes.Length < outputLength) throw new ArgumentException("The output span is too small.");
				bytes = bytes.Slice(0, outputLength);

				bytesWritten = outputLength;

				// Decode complete blocks
				while (chars.Length >= 11 << shiftCount)
				{
					DecodeBlock(reverseAlphabet, chars, shiftCount, bytes);
					chars = chars.Slice(11 << shiftCount);
					bytes = bytes.Slice(8);
				}

				if (chars.IsEmpty) return true;

				// Decode the final (incomplete) block
				{
					DecodeBlock(reverseAlphabet, chars, shiftCount, bytes);
					return true;
				}
			}
			catch (ArgumentException)
			{
				bytesWritten = 0;
				return false;
			}
		}

		/// <summary>
		/// Decodes the given block of up to 11 Base62 characters, in UTF-8 or UTF-16 form (shift count 0 or 1 respectively) into the corresponding number of bytes.
		/// </summary>
		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static void DecodeBlock(ReadOnlySpan<sbyte> reverseAlphabet, ReadOnlySpan<byte> chars11, int shiftCount, Span<byte> bytes)
		{
			System.Diagnostics.Debug.Assert(reverseAlphabet.Length == 128);
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);
			System.Diagnostics.Debug.Assert(chars11.Length >= 2 << shiftCount);
			System.Diagnostics.Debug.Assert(bytes.Length >= 1);

			// Can decode 11 chars back into 8 bytes
			var ulongValue = 0UL;
			var max = Math.Min(chars11.Length, 11 << shiftCount);
			var increment = 1 << shiftCount;
			for (var i = 0; i < max; i += increment) // Chars are little-endian, so the left byte of each pair contains the value if operating on chars
			{
				var chr = chars11[i];
				var value = (ulong)reverseAlphabet[chr]; // -1 (invalid character) becomes UInt64.MaxValue
				if (value >= 62) throw new ArgumentException("The input is not valid Base62.");

				ulongValue = ulongValue * 62 + value;
			}

			max = Math.Min(8, bytes.Length) - 1;
			for (var i = max; i >= 0; i--)
			{
				bytes[i] = (byte)ulongValue;
				ulongValue >>= 8;
			}
		}
	}
}
