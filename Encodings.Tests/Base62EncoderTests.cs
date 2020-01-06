using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Architect.Encodings.Tests
{
	public sealed class Base62EncoderTests
	{
		// #TODO: Nullable reference types
		[Fact]
		public void TryToBase62Chars_WithNullArguments_ShouldOutput0Chars()
		{
			// Because the null byte[] is converted to an empty span, the result should be empty
			byte[] input = null;
			byte[] output = null;
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(input, output, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(0, charsWritten);
		}

		[Fact]
		public void TryToBase62Chars_WithNonEmptyInputAndNullOutputArgument_ShouldReturnFalse()
		{
			// Because the null byte[] is converted to an empty span but 2 output bytes are needed, this should return false
			byte[] output = null;
			var result = Base62Encoder.TryToBase62Chars(stackalloc byte[1], output, out _);
			Assert.False(result);
		}

		[Fact]
		public void TryFromBase62Chars_WithNullArguments_ShouldOutput0Chars()
		{
			// Because the null byte[] is converted to an empty span, the result should be empty
			byte[] input = null;
			byte[] output = null;
			var encodingSucceeded = Base62Encoder.TryFromBase62Chars(input, output, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(0, charsWritten);
		}

		[Fact]
		public void TryFromBase62Chars_WithNonEmptyInputAndNullOutputArgument_ShouldReturnFalse()
		{
			// Because the null byte[] is converted to an empty span but 1 output byte is needed, decoding fails
			byte[] output = null;
			var result = Base62Encoder.TryFromBase62Chars(stackalloc byte[2], output, out _);
			Assert.False(result);
		}

		[Theory]
		[InlineData(1)]
		[InlineData(4)]
		[InlineData(8)]
		[InlineData(41)]
		public void TryFromBase62Chars_WithInvalidLengthInput_ShouldReturnFalse(int base62Length)
		{
			Span<byte> input = stackalloc byte[base62Length];
			input.Fill((byte)'0');
			var result = Base62Encoder.TryFromBase62Chars(input, stackalloc byte[base62Length], out _);
			Assert.False(result);
		}

		[Theory]
		[InlineData("Ω")]
		[InlineData("💩")]
		[InlineData("\0\0")]
		[InlineData("  ")]
		[InlineData("AAA AAA")]
		public void TryFromBase62Chars_WithInvalidCharInput_ShouldReturnFalse(string invalidBase62String)
		{
			var chars = invalidBase62String.ToCharArray();
			var outputLength = Base62Encoder.GetBase62Length(chars.Length);
			var result = Base62Encoder.TryFromBase62Chars(chars, stackalloc byte[outputLength], out _);
			Assert.False(result);
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryToBase62Chars_WithByteSpanOutput_ShouldOutputExpectedChars(string text, string base62String)
		{
			var expectedOutput = Encoding.ASCII.GetBytes(base62String).AsSpan();
			var inputSpan = Encoding.UTF8.GetBytes(text).AsSpan();
			Span<byte> outputSpan = stackalloc byte[base62String.Length];
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(inputSpan, outputSpan, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(base62String.Length, charsWritten);
			Assert.True(expectedOutput.SequenceEqual(outputSpan));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryToBase62Chars_WithCharSpanOutput_ShouldOutputExpectedChars(string text, string base62String)
		{
			var expectedOutput = base62String.AsSpan();
			var inputSpan = Encoding.UTF8.GetBytes(text).AsSpan();
			Span<char> outputSpan = stackalloc char[base62String.Length];
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(inputSpan, outputSpan, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(base62String.Length, charsWritten);
			Assert.True(expectedOutput.SequenceEqual(outputSpan));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void ToBase62String_Regularly_ShouldReturnExpectedString(string text, string base62String)
		{
			var inputSpan = Encoding.UTF8.GetBytes(text).AsSpan();
			var result = Base62Encoder.ToBase62String(inputSpan);
			Assert.Equal(base62String, result);
			Assert.Equal(text.Length, result.Length * 8 / 11);
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void ToBase62String_WithByteArrayInput_ShouldReturnExpectedString(string text, string base62String)
		{
			var inputBytes = Encoding.UTF8.GetBytes(text);
			var result = Base62Encoder.ToBase62String(inputBytes);
			Assert.Equal(base62String, result);
			Assert.Equal(text.Length, result.Length * 8 / 11);
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void ToBase62String_WithPartialByteArrayInput_ShouldReturnExpectedString(string text, string base62String)
		{
			var inputBytes = Encoding.UTF8.GetBytes($"_{text}_");
			var result = Base62Encoder.ToBase62String(inputBytes, 1, inputBytes.Length - 2);
			Assert.Equal(base62String, result);
			Assert.Equal(text.Length, result.Length * 8 / 11);
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryFromBase62Chars_WithByteSpanInput_ShouldOutputExpectedBytes(string text, string base62String)
		{
			var expectedOutput = Encoding.UTF8.GetBytes(text).AsSpan();
			var inputSpan = Encoding.ASCII.GetBytes(base62String).AsSpan();
			Span<byte> outputSpan = stackalloc byte[text.Length];
			var encodingSucceeded = Base62Encoder.TryFromBase62Chars(inputSpan, outputSpan, out var bytesWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(text.Length, bytesWritten);
			Assert.True(expectedOutput.SequenceEqual(outputSpan));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryFromBase62Chars_WithCharSpanInput_ShouldOutputExpectedBytes(string text, string base62String)
		{
			var expectedOutput = Encoding.UTF8.GetBytes(text).AsSpan();
			var inputSpan = base62String.AsSpan();
			Span<byte> outputSpan = stackalloc byte[text.Length];
			var encodingSucceeded = Base62Encoder.TryFromBase62Chars(inputSpan, outputSpan, out var bytesWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(text.Length, bytesWritten);
			Assert.True(expectedOutput.SequenceEqual(outputSpan));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryFromBase62String_Regularly_ShouldOutputExpectedByteArray(string text, string base62String)
		{
			var expectedOutput = Encoding.UTF8.GetBytes(text).AsSpan();
			Span<byte> output = stackalloc byte[expectedOutput.Length];
			var result = Base62Encoder.TryFromBase62String(base62String, output, out var bytesWritten);
			Assert.True(result);
			Assert.Equal(expectedOutput.Length, bytesWritten);
			Assert.True(expectedOutput.SequenceEqual(output));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void FromBase62String_Regularly_ShouldReturnExpectedByteArray(string text, string base62String)
		{
			var expectedOutput = Encoding.UTF8.GetBytes(text);
			var result = Base62Encoder.FromBase62String(base62String);
			Assert.Equal(expectedOutput, result);
		}

		[Theory]
		[InlineData("")]
		[InlineData("1")]
		[InlineData("123")]
		[InlineData("1234")]
		[InlineData("12345")]
		[InlineData("123456")]
		[InlineData("1234567")]
		[InlineData("12345678")]
		[InlineData("123456789")]
		[InlineData("1234567890")]
		[InlineData("12345678901")]
		[InlineData("123456789012")]
		[InlineData("1234567890123456789012345678901")] // 31 characters (one block)
		[InlineData("12345678901234567890123456789012")] // 32 characters (one block)
		[InlineData("123456789012345678901234567890123")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")] // 8x32
		public void TryToBase62CharsAndTryFromBase62Chars_Regularly_ShouldProvideOriginalText(string text)
		{
			var inputChars = Encoding.UTF8.GetBytes(text).AsSpan();
			Span<byte> outputChars = stackalloc byte[inputChars.Length];

			Span<byte> bytes = stackalloc byte[Base62Encoder.GetBase62Length(inputChars.Length)];
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(inputChars, bytes, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(bytes.Length, charsWritten);

			var decodingSucceeded = Base62Encoder.TryFromBase62Chars(bytes, outputChars, out var bytesWritten);
			Assert.True(decodingSucceeded);
			Assert.Equal(outputChars.Length, bytesWritten);

			Assert.True(inputChars.SequenceEqual(outputChars));
		}

		[Theory]
		[InlineData(0UL)]
		[InlineData(1UL << 0)]
		[InlineData(1UL << 1)]
		[InlineData(1UL << 2)]
		[InlineData(1UL << 3)]
		[InlineData(1UL << 4)]
		[InlineData(1UL << 5)]
		[InlineData(1UL << 6)]
		[InlineData(1UL << 7)]
		[InlineData(1UL << 8)]
		[InlineData(1UL << 9)]
		[InlineData(1UL << 10)]
		[InlineData(1UL << 31)]
		[InlineData(1UL << 63)]
		[InlineData((1UL << 31) - 12345)]
		[InlineData((1UL << 63) - 12345)]
		[InlineData(UInt64.MaxValue)]
		public void TryToBase62CharsAndTryFromBase62Chars_WithUlongValue_ShouldProvideOriginalText(ulong input)
		{
			var ulongs = MemoryMarshal.CreateReadOnlySpan(ref input, 1);
			var inputBytes = MemoryMarshal.AsBytes(ulongs);
			Span<byte> outputChars = stackalloc byte[inputBytes.Length];

			Span<byte> bytes = stackalloc byte[Base62Encoder.GetBase62Length(inputBytes.Length)];
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(inputBytes, bytes, out var charsWritten);
			Assert.True(encodingSucceeded);
			Assert.Equal(bytes.Length, charsWritten);

			var decodingSucceeded = Base62Encoder.TryFromBase62Chars(bytes, outputChars, out var bytesWritten);
			Assert.True(decodingSucceeded);
			Assert.Equal(outputChars.Length, bytesWritten);

			Assert.True(inputBytes.SequenceEqual(outputChars));
		}

		[Theory]
		[InlineData("")]
		[InlineData("1")]
		[InlineData("123")]
		[InlineData("1234")]
		[InlineData("12345")]
		[InlineData("123456")]
		[InlineData("1234567")]
		[InlineData("12345678")]
		[InlineData("123456789")]
		[InlineData("1234567890")]
		[InlineData("12345678901")]
		[InlineData("123456789012")]
		[InlineData("1234567890123456789012345678901")] // 31 characters (one block)
		[InlineData("12345678901234567890123456789012")] // 32 characters (one block)
		[InlineData("123456789012345678901234567890123")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")] // 8x32
		public void TryToBase62CharsAndTryFromBase62Chars_WithCustomAlphabet_ShouldProvideOriginalText(string text)
		{
			// Use chars 66 through 127, to see if that works
			var alphabet = new Base62Alphabet(Enumerable.Range(127 - 61, 62).Select(i => (byte)i).ToArray());

			var inputChars = Encoding.UTF8.GetBytes(text).AsSpan();
			Span<byte> outputChars = stackalloc byte[inputChars.Length];

			Span<byte> bytes = stackalloc byte[Base62Encoder.GetBase62Length(inputChars.Length)];
			var encodingSucceeded = Base62Encoder.TryToBase62Chars(inputChars, bytes, out var charsWritten, alphabet);
			Assert.True(encodingSucceeded);
			Assert.Equal(bytes.Length, charsWritten);

			var decodingSucceeded = Base62Encoder.TryFromBase62Chars(bytes, outputChars, out var bytesWritten, alphabet);
			Assert.True(decodingSucceeded);
			Assert.Equal(outputChars.Length, bytesWritten);

			Assert.True(inputChars.SequenceEqual(outputChars));
		}

		[Theory]
		[InlineData("", "")]
		[InlineData("1", "0n")]
		[InlineData("123", "0DWjr")]
		[InlineData("124", "0DWjs")]
		[InlineData("1234", "0trBLg")]
		[InlineData("12345", "3idarWH")]
		[InlineData("123456", "0FMJUCzH4")]
		[InlineData("1234567", "11Q8Sjd0TP")]
		[InlineData("12345678", "4DruweP3xQ8")]
		[InlineData("123456789", "4DruweP3xQ80v")]
		[InlineData("1234567890", "4DruweP3xQ83o8")] // 10 characters
		[InlineData("12345678901", "4DruweP3xQ80Fizp")] // 11 characters
		[InlineData("123456789012", "4DruweP3xQ812vnHO")] // 12 characters
		[InlineData("1234567890123456789012345678901", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC16b6INYIiX")] // 31 characters
		[InlineData("12345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 32 characters
		[InlineData("123456789012345678901234567890123", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt40p")] // 32 + 1 characters
		[InlineData("1234567890123456789012345678901212345678901234567890123456789012", "4DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt44DruweP3xQ84uPWdXzTsO64jvpVkbvvYC4ZFBztFdHt4")] // 2x32 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB1", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni0n")] // 2x32 + 1 characters
		[InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", "5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5bLib8myyOX5bLib8myyOX5bLib8myyOX5bLib8myyOX5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni5gh2MS08Uni")] // 8x32 characters
		public void TryToBase62Chars_WithCustomAlphabet_ProvidesDifferentOutputThanRegularAlphabet(string text, string regularBase62String)
		{
			// Use chars 66 through 127, to see if that works
			var alphabet = new Base62Alphabet(Enumerable.Range(127 - 61, 62).Select(i => (byte)i).ToArray());

			var inputChars = Encoding.UTF8.GetBytes(text);
			Span<byte> outputChars = stackalloc byte[inputChars.Length];

			var customBase62String = Base62Encoder.ToBase62String(inputChars, alphabet);

			if (text.Length > 0)
				Assert.NotEqual(regularBase62String, customBase62String);
		}
	}
}
