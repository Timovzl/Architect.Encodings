using System;
using System.Linq;

namespace Architect.Encodings
{
	public sealed class Base62Alphabet
	{
		public override bool Equals(object obj) => obj is Base62Alphabet other && other.ForwardAlphabet.SequenceEqual(this.ForwardAlphabet);
		public override int GetHashCode() => this.ForwardAlphabet[0].GetHashCode() ^ this.ForwardAlphabet[61].GetHashCode();

		public ReadOnlySpan<byte> ForwardAlphabet => this._alphabet;
		private readonly byte[] _alphabet;

		public ReadOnlySpan<sbyte> ReverseAlphabet => this._reverseAlphabet.AsSpan();
		private readonly sbyte[] _reverseAlphabet;

		/// <summary>
		/// Constructs a Base62 alphabet, including its reverse representation.
		/// The result should be cached for reuse.
		/// </summary>
		public Base62Alphabet(ReadOnlySpan<byte> alphabet)
		{
			if (alphabet.Length != 62) throw new ArgumentException("Expected an alphabet of length 62.");

			this._alphabet = alphabet.ToArray();

			if (this._alphabet.Any(chr => chr == 0))
				throw new ArgumentException("The NULL character is not allowed.");
			if (this._alphabet.Any(chr => chr > 127))
				throw new ArgumentException("Non-ASCII characters are not allowed.");
			if (this._alphabet.Distinct().Count() != this._alphabet.Length)
				throw new ArgumentException("All characters in the alphabet must be distinct.");

			this._reverseAlphabet = GetReverseAlphabet(this.ForwardAlphabet);

			System.Diagnostics.Debug.Assert(this.ReverseAlphabet.Length == 128);
		}

		/// <summary>
		/// <para>
		/// Creates a reverse alphabet for the given alphabet.
		/// </para>
		/// <para>
		/// When indexing into the slot matching a character's numeric value, the result is the value between 0 and 61 (inclusive) represented by the character.
		/// (Slots not related to any of the alphabet's characters contain -1.)
		/// </para>
		/// <para>
		/// The result should be cached for reuse.
		/// </para>
		/// </summary>
		internal static sbyte[] GetReverseAlphabet(ReadOnlySpan<byte> alphabet)
		{
			if (alphabet.Length != 62) throw new ArgumentException("Expected an alphabet of length 62.");

			var result = new sbyte[128];
			Array.Fill(result, (sbyte)-1);
			for (sbyte i = 0; i < alphabet.Length; i++) result[alphabet[i]] = i;
			return result;
		}
	}
}
