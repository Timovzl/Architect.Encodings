using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Architect.Encodings
{
	/// <summary>
	/// A Base62 implementation that encodes chunks of 256 bits (32 bytes) into 43 characters.
	/// </summary>
	// TODO: Output endianness is undesirable: changes inside a block do not affect the expected characters in the output
	[Obsolete("For documentation purposes.")]
	internal static class Base62Encoder256
	{
		private static Base62Alphabet DefaultAlphabet { get; } = new Base62Alphabet(Encoding.ASCII.GetBytes("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"));

		/// <summary>
		/// A vector filled with the ushort value 127, the maximum ASCII byte value.
		/// </summary>
		private static Vector<ushort> VectorOf127s = new Vector<ushort>(127);

		static Base62Encoder256()
		{
			if (!BitConverter.IsLittleEndian)
				throw new NotSupportedException("Big-endian architectures are not currently supported.");
		}

		public static int GetBase62Length(int byteLength)
		{
			// Avoid overflow
			const int MaxLength = (Int32.MaxValue - 31) / 43;
			if (byteLength > MaxLength) throw new ArgumentOutOfRangeException(nameof(byteLength));

			// By adding 31 (i.e. 32 - 1), we get the ceiling of the division
			return (byteLength * 43 + 31) / 32;
		}
		
		/// <summary>
		/// Returns the number of bytes represented by the given number of Base62 characters.
		/// Throws if the given number is not a valid Base62 length.
		/// </summary>
		public static int GetByteLength(int base62Length)
		{
			// Avoid overflow
			const int MaxLength = Int32.MaxValue / 32;
			if (base62Length > MaxLength) throw new ArgumentOutOfRangeException(nameof(base62Length));

			var result = base62Length * 32 / 43;
			if (GetBase62Length(result) != base62Length) throw new ArgumentException("The input length was not a valid Base62 length.");
			return result;
		}

		public static string ToBase62String(byte[] bytes, Base62Alphabet alphabet = null)
		{
			return ToBase62String(bytes.AsSpan(), alphabet);
		}

		public static string ToBase62String(byte[] bytes, int offset, int length, Base62Alphabet alphabet = null)
		{
			return ToBase62String(bytes.AsSpan(offset, length), alphabet);
		}

		public static string ToBase62String(ReadOnlySpan<byte> bytes, Base62Alphabet alphabet = null)
		{
			// We need a temporary place to store bytes, since we get UTF8 but want a string
			// That is alright:
			// We cannot use String.Create with span input anyway, so we need to make a full copy somewhere - it might as well be here

			// Prefer stackalloc over renting
			var outputLength = GetBase62Length(bytes.Length);
			byte[] charArray = null;
			var chars = outputLength <= 1024
				? stackalloc byte[outputLength]
				: (charArray = ArrayPool<byte>.Shared.Rent(outputLength));
			try
			{
				TryToBase62Chars(bytes, chars, out _, alphabet); // Output is always true
				return Encoding.ASCII.GetString(chars);
			}
			finally
			{
				if (charArray != null) ArrayPool<byte>.Shared.Return(charArray);
			}
		}

		public static bool TryToBase62Chars(ReadOnlySpan<byte> bytes, Span<byte> chars, out int charsWritten, Base62Alphabet alphabet = null)
		{
			return TryToBase62Chars(bytes, chars, shiftCount: 0, out charsWritten, alphabet);
		}
		public static bool TryToBase62Chars(ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten, Base62Alphabet alphabet = null)
		{
			var charsAsBytes = MemoryMarshal.AsBytes(chars);
			return TryToBase62Chars(bytes, charsAsBytes, shiftCount: 1, out charsWritten, alphabet);
		}

		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static bool TryToBase62Chars(ReadOnlySpan<byte> bytes, Span<byte> chars, int shiftCount, out int charsWritten, Base62Alphabet alphabet = null)
		{
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);

			var forwardAlphabet = (alphabet ?? DefaultAlphabet).ForwardAlphabet;

			charsWritten = 0;

			var outputLength = GetBase62Length(bytes.Length);
			if (chars.Length < outputLength << shiftCount) return false;
			chars = chars.Slice(0, outputLength << shiftCount);

			charsWritten = outputLength;

			Span<uint> workspace = stackalloc uint[8];
			var workspaceBytes = MemoryMarshal.AsBytes(workspace);

			// Encode complete blocks
			while (bytes.Length >= 32)
			{
				EncodeBlock(forwardAlphabet, workspace, workspaceBytes, bytes, chars, shiftCount);
				bytes = bytes.Slice(32);
				chars = chars.Slice(43 << shiftCount);
			}

			if (bytes.IsEmpty) return true;

			// Encode the final (incomplete) block
			{
				Span<byte> finalInputBlock = stackalloc byte[32];
				Span<byte> finalOutputBlock = stackalloc byte[43 << shiftCount];
				bytes.CopyTo(finalInputBlock);
				EncodeBlock(forwardAlphabet, workspace, workspaceBytes, finalInputBlock, finalOutputBlock, shiftCount);
				finalOutputBlock.Slice(0, chars.Length).CopyTo(chars);
				return true;
			}
		}

		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static void EncodeBlock(ReadOnlySpan<byte> alphabet, Span<uint> workspace, Span<byte> workspaceBytes,
			ReadOnlySpan<byte> bytes32, Span<byte> output, int shiftCount)
		{
			System.Diagnostics.Debug.Assert(alphabet.Length == 62);
			System.Diagnostics.Debug.Assert(workspace.Length == 8);
			System.Diagnostics.Debug.Assert(workspaceBytes.Length == 32);
			System.Diagnostics.Debug.Assert(bytes32.Length >= 32);
			System.Diagnostics.Debug.Assert(output.Length >= 43 << shiftCount);

			// Create a workspace of 8 uints that we will gradually divide
			bytes32.Slice(0, 32).CopyTo(workspaceBytes);

			// Can encode 32 bytes as 43 chars
			var max = 43 << shiftCount;
			var increment = 1 << shiftCount;
			for (var i = 0; i < max; i += increment) // Little-endian, so the left byte contains the value
			{
				DivideInPlace(workspace, out var remainder);
				output[i] = alphabet[(int)remainder];
			}
		}

		private static void DivideInPlace(Span<uint> bytesAs8Uints, out ulong remainder)
		{
			System.Diagnostics.Debug.Assert(bytesAs8Uints.Length == 8);

			const ulong Divisor = 62;

			var buffer = 0UL;

			// From MSB to LSB (little-endian)
			// TODO Enhancement: Unroll for performance?
			for (var i = 8 - 1; i >= 0; i--)
			{
				// Existing buffer is shifted, and the next portion is added
				buffer = (buffer << 32) | bytesAs8Uints[i];

				var quotient = buffer / Divisor;

				// Make the buffer equal to the modulus, which is the same as subtracting what was divided away
				buffer -= quotient * Divisor;

				System.Diagnostics.Debug.Assert(buffer < Divisor);

				// Keep the result of the division in the uint
				bytesAs8Uints[i] = (uint)quotient;
			}

			// What remains in the buffer and cannot be divided is the remainder
			remainder = buffer;

			System.Diagnostics.Debug.Assert(remainder < Divisor);
		}

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

		public static bool TryFromBase62String(string base62String, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			return TryFromBase62Chars(base62String.AsSpan(), bytes, out bytesWritten, alphabet);
		}

		public static bool TryFromBase62Chars(ReadOnlySpan<byte> chars, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
		{
			return TryFromBase62Chars(chars, shiftCount: 0, bytes, out bytesWritten, alphabet);
		}

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
			return TryFromBase62Chars(charsAsBytePairs, shiftCount: 1, bytes, out bytesWritten, alphabet);
		}

		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static bool TryFromBase62Chars(ReadOnlySpan<byte> chars, int shiftCount, Span<byte> bytes, out int bytesWritten, Base62Alphabet alphabet = null)
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

				Span<uint> workspace = stackalloc uint[8];
				var workspaceBytes = MemoryMarshal.AsBytes(workspace);

				// Decode complete blocks
				while (chars.Length >= 43 << shiftCount)
				{
					DecodeBlock(reverseAlphabet, workspace, workspaceBytes, chars, shiftCount, bytes);
					workspaceBytes.Clear();
					chars = chars.Slice(43 << shiftCount);
					bytes = bytes.Slice(32);
				}

				if (chars.IsEmpty) return true;

				// Decode the final (incomplete) block
				{
					Span<byte> finalInputBlock = stackalloc byte[43 << shiftCount];
					Span<byte> finalOutputBlock = stackalloc byte[32];
					finalInputBlock.Fill(alphabet.ForwardAlphabet[0]); // Prefill with the character that represents 0
					chars.CopyTo(finalInputBlock);
					DecodeBlock(reverseAlphabet, workspace, workspaceBytes, finalInputBlock, shiftCount, finalOutputBlock);
					finalOutputBlock.Slice(0, bytes.Length).CopyTo(bytes);
					return true;
				}
			}
			catch (ArgumentException)
			{
				bytesWritten = 0;
				return false;
			}
		}

		/// <param name="shiftCount">When reading bytes, left shift the index by 0 (no change). When reading chars, left shift it by 1 (doubling it).</param>
		private static void DecodeBlock(ReadOnlySpan<sbyte> reverseAlphabet, Span<uint> workspace, Span<byte> workspaceBytes,
			ReadOnlySpan<byte> chars43, int shiftCount, Span<byte> output)
		{
			System.Diagnostics.Debug.Assert(reverseAlphabet.Length == 128);
			System.Diagnostics.Debug.Assert(workspace.Length == 8);
			System.Diagnostics.Debug.Assert(workspaceBytes.Length == 32);
			System.Diagnostics.Debug.Assert(shiftCount == 0 || shiftCount == 1);
			System.Diagnostics.Debug.Assert(chars43.Length >= 43 << shiftCount);
			System.Diagnostics.Debug.Assert(output.Length >= 32);

			// Can decode 43 chars back into 32 bytes
			var initial = (43 - 1) << shiftCount;
			var decrement = 1 << shiftCount;
			for (var i = initial; i >= 0; i -= decrement) // Little-endian, so the left byte contains the value
			{
				var chr = chars43[i];
				var value = reverseAlphabet[chr];
				if (value < 0 || value >= 62) throw new ArgumentException("The input is not valid Base62.");
				MultiplyInPlace(workspace, remainderOfDivision: (uint)value);
			}

			workspaceBytes.CopyTo(output);
		}

		private static void MultiplyInPlace(Span<uint> bytesAs8Uints, uint remainderOfDivision)
		{
			System.Diagnostics.Debug.Assert(bytesAs8Uints.Length == 8);

			const ulong Multiplier = 62;

			var buffer = 0UL;

			// From LSB to MSB (little-endian)
			var i = 0;
			do
			{
				buffer = bytesAs8Uints[i] * Multiplier + (buffer >> 32);

				bytesAs8Uints[i] = (uint)buffer;
			} while (++i < 8 && buffer > 0UL); // Short-circuit once buffer is zero (nothing more traveling towards MSB)

			bytesAs8Uints[0] += remainderOfDivision;
		}
	}
}
