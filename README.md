# Architect.Encodings
Provides various mechanisms for encoding binary data.

## Base64

_Space: 4/3 (~133%) compared to binary_

Since Base64 is included in .NET through the Convert class, it is not included in this library. However, it is mentioned for completeness of this overview.

## Base64URL

_Space: 4/3 (~133%) compared to binary_

TODO

## Base32

_Space: 8/5 (160%) compared to binary_

TODO

## Base16 (Hexadecimal)

_Space: 2/1 (200%) compared to binary_

TODO

## Base62 (Alphanumeric)

_Space (in practice): 11/8 (137.5%) compared to binary_

_Space (in theory): lg(62)/lg(256) (~134%) compared to binary_

When only alphanumeric characters are acceptable, yet Base32 is not compact enough, Base62 is the go-to encoding.

Since there is no official standard for Base62 yet, I have proposed the carefully designed structure outlined below to the [Internet Engineering Task Force](https://ietf.org/).

Base62 uses the characters of Base64, except for its symbols and padding character, and in a different order. The alphabet, representing the values 0 through 61 respectively, is 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz. It follows ASCII order.

The reduced character set leaves Base62 in need of slightly more space, but more importantly, much harder to implement. Whereas Base64 represents 6 bits per character (and Base32 represents 5 bits per character), Base62 represents about 5.95419631039 bits per character, in theory. This is a challenge, as it is hard to represent partial bits. Rather, Base62 interprets a series of bytes as a large integer in base-2 and converts it to base-62 using repeated integer divisions, where the remainders form the Base62 character values.

Since there is no power of 62 that coincides with a power of 2, there is no non-zero number of bytes that requires exactly a _whole_ number of Base62 characters to represent. And since integer divisions on unbounded input lengths are infeasible, that introduces the need for an imperfect block size. For example, a block of 8 bytes requires about 10.7487 characters in theory. In practice, we might use 11 characters to represent it, wasting a bit of space.

**The first question**, then, is what the block size should be. Note that a larger block size has the potential to reduce the storage overhead. A smaller block size, on the other hand, can make the required integer divisions easier to implement and more performant.

There are two viable options: 8 bytes (64 bits), or 32 bytes (256 bits).

8 bytes is the largest block size for which the integer divisions are a walk in the park: we can simply use unsigned 64 bit integers (ulongs). The Base62 implementation becomes reasonably simple. 8 bytes can be represented by 11 characters, for a ratio of 11/8, or 137.5%. This ratio has a space overhead of about 2.3% compared to the theoretical optimum.

32 bytes is the next reasonable storage ratio improvement: 16 bytes would need 22 characters (no improvement), 24 bytes would need 33 characters (no improvement), whereas 32 bytes need only 43 (rather than 44) characters. This makes for a storage ratio that is very close to the theoretical optimum: 43/32, or 134.375%. The space overhead is only about 0.00012%, or a tenth of a permille, compared to the theoretical optimum. The implementation, however, is not trivial. The integer divisions need to be done on 256 bits at a time. A BigInteger type could be used, but performance would be significantly worse, and portability of the standard would degrade tremendously. A UInt256 type could perform better but is not readily available. The limited set of required operations could be implemented manually. This has been tested, but performed around a factor 8 worse than the 8-byte implementation.

Considering that the 32-byte implementation is less performant and much harder to implement, can we disregard it? Let's consider how many characters it could save compared to the 8-byte implementation. When encoding larger pieces of data (such as asymmetric cryptographic keys), we know that it saves about 1 character of each 44 characters. Generally, we can reason that larger pieces of data do not come with the alphanumeric requirement, and can use Base64 instead. It is generally the shorter pieces of data that benefit from Base62 encoding. For example, any 8-byte ulong can be represented in alphanumeric as only 11 characters - quite good compared to the 20 required to represent it in the decimal system. We note that the 32-byte implementation has _no space benefit over the 8-byte implementation_ for the following common short data lengths: 0 through 10, specifically 8 (ulong), 12, 16, 24.

Taking all of the above into consideration, we can consider the **8-byte implementation** to be the superior standard.

**The second question** is which bytes should be represented by which characters. For intuitive purposes, each character should be in the same relative position as the byte(s) it represents. Therefore, changes to the input data will be reflected by similar changes to the output data.

Let's look at a few examples.

- [] is encoded as ""
- [49] is encoded as "0n"
- [49, 50, 51, 52, 53, 54, 55, 56, 57, 48, 49] is encoded as "4DruweP3xQ80Fizp"

Blocks shorter than 11 bytes omit characters that they do not require.

What happens if we increase the last byte's value by 1?

- [49, 50, 51] is encoded as "0DWjr"
- [49, 50, 52] is encoded as "0DWjs"
- [49, 50, 255] is encoded as "0DWn9"

We can see that the change to the last byte affects the last character pair. The delta of only 1 increases "jr" to "js", whereas the delta of 203 increases "js" to "n9", affecting the higher order character of the pair as well. Note that none of the characters to the left are related to the third byte's value.

- [49, 50, 51] is encoded as "0DWjr"
- [255, 50, 51] is encoded as "18AoV"

So why was the entire block affected when we only changed the first byte's value? This is because we are treating the block as a large integer, which we repeatedly divide by 62. A change anywhere in a block can affect any output to right as well. Let's see that confirmed when we change only the middle byte.

- [49, 50, 51] is encoded as "0DWjr"
- [49, 255, 51] is encoded as "0DkOJ"

Indeed, any characters to the right of the change are affected as well, but characters to its left are not. Luckily, this effect is bounded by its block:

- [49, 49, 49, 49, 49, 49, 49, 49, 50, 50, 50, 50, 50, 50, 50, 50] is encoded as "4DqcOBOWbth 4JBw9Ubg8Is" (space for explanation purposes only)
- [88, 49, 49, 49, 49, 49, 49, 49, 50, 50, 50, 50, 50, 50, 50, 50] is encoded as "7ZRZjtdklpx 4JBw9Ubg8Is" (space for explanation purposes only)

We have 8 bytes with value 49 (the first block) and another 8 with value 50 (the second block). When we change the first 49 value to 88, we see that the entire first output block (i.e. the first 11 characters) changes completely, while the second block remains unaffected.
