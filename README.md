# Architect.Encodings
Provides various mechanisms for encoding binary data.

## Base64

_Ratio: 8/6 (~133%) compared to binary_

Since Base64 is included in .NET through the Convert class, it is not included in this library. However, it is mentioned for completeness of this overview.

## Base32

TODO

## Base16 (Hexadecimal)

TODO

## Base62 (Alphanumeric)

_Ratio (practice): 11/8 (137.5%) compared to binary_

_Ratio (theory): lg(62)/lg(256) (~134%) compared to binary_

When only alphanumeric characters are acceptable, yet Base32 is not compact enough, Base62 is the go-to encoding.

Since there is no official standard for Base62, I have proposed the carefully crafted structure outlined here to the [Internet Engineering Task Force](https://ietf.org/).

Base62 uses the characters of Base64, except the symbols and the padding character. That leaves it requiring slightly more space, but more importantly, much harder to implement. Whereas Base64 represents 6 bits per character (and Base32 represents 5 bits per character), Base62 represents about 5.95419631039 bits per character. This is a challenge, as it is hard to represent partial bits. Rather, Base62 interprets a series of bytes as a large integer in base-2 and converts it to base-62 using integer division.

Since there is no power of 62 that coincides with a power of 2, there is no non-zero number of bytes that requires exactly a _whole_ number of Base62 characters to represent. And since integer divisions on unbounded input lengths are infeasible, that introduces the need for an imperfect block size. For example, a block of 8 bytes requires about 10.7487 characters in theory. In practice, we might use 11 characters to represent it, wasting a bit of space.

**The first question**, then, is what the block size should be. Note that a larger block size has the potential to reduce the storage overhead. A smaller block size, on the other hand, can make the required integer divisions easier to implement and more performant.

There are two viable options: 8 bytes (64 bits), or 32 bytes (256 bits).

8 bytes is the largest block size for the integer divisions are a walk in the park: we can simply use unsigned 64 bit integers (ulongs). The Base62 implementation becomes reasonably simple. 8 bytes can be represented by 11 characters, for a ratio of 11/8, or 137.5%. This ratio has a space overhead of about 2.3% compared to the theoretical optimum.

32 bytes is the next reasonable storage ratio improvement: 16 bytes would need 22 characters (no improvement), while 32 bytes need only 43 (rather than 44) characters. This makes for a storage ratio that is very close to the theoretical optimum: 43/32, or 134.375%. The space overhead is only 0.00012%, or a tenth of a permille, compared to the theoretical optimum. The implementation, however, is not trivial. The integer divisions need to be done on 256 bits at a time. A BigInteger type could be used, but performance would be significantly worse, and portability of the standard would degrade tremendously. A UInt256 type would perform better, but is not readily available. The limited set of required operations could be implemented manually. This has been tested, but performed about a factor 8 worse than the 8-byte implementation.

Considering that the 32-byte implementation is less performant and much harder to implement, can we disregard it? Let's consider how many characters it could save compared to the 8-byte implementation. When encoding larger pieces of data (such as asymmetric cryptographic keys), we know that it saves about 1 character of each 44 characters. More importantly, we can reason that larger pieces of data generally do not come with the alphanumeric requirement, and can generally use Base64. It is generally shorter pieces of data that benefit from Base62 encoding. For example, any 8-byte ulong can be represented in alphanumeric as only 11 characters - quite good compared to the 20 required to represent it in the decimal system. With this in mind, we notice that the 32-byte implementation has _no space benefit over the 8-byte implementation_ for the following common data lengths: 0-10, 8 (ulong, specifically), 12, 16, 24.

With all the above in mind, we can consider the **8-byte implementation** to be the superior standard.

**The second question** is which bytes should be represented by which characters. For intuitive purposes, each character should be in the same relative position as the byte(s) it represents. Therefore, changes to the input data will be reflected by similar changes to the output data. For example, if the last byte's value is increased by 1, the last one or two output characters will represent that increase by 1. (It should be noted that, because we perform integer divisions on blocks of 8 bytes / 11 characters, any characters in the same block that are to the _right_ of the directly affected may be affected as well.)
