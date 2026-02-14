using System.Numerics;
using System.Security.Cryptography;

namespace ZipZap.Sftp.Ext;

public static class BigIntegerExt {
    extension(BigInteger) {
        public static BigInteger Random(BigInteger upperBound) {
            BigInteger value;
            var bytes = upperBound.ToByteArray();

            // count how many bits of the most significant byte are 0
            // NOTE: sign bit is always 0 because `max` must always be positive
            byte zeroBitsMask = 0b00000000;

            var mostSignificantByte = bytes[^1];

            // we try to set to 0 as many bits as there are in the most significant byte, starting from the left (most significant bits first)
            // NOTE: `i` starts from 7 because the sign bit is always 0
            for (var i = 7; i >= 0; i--) {
                // we keep iterating until we find the most significant non-0 bit
                if ((mostSignificantByte & (0b1 << i)) != 0) {
                    var zeroBits = 7 - i;
                    zeroBitsMask = (byte)(0b11111111 >> zeroBits);
                    break;
                }
            }

            do {
                bytes = RandomNumberGenerator.GetBytes(bytes.Length);

                // set most significant bits to 0 (because `value > max` if any of these bits is 1)
                bytes[^1] &= zeroBitsMask;

                value = new BigInteger(bytes);

                // `value > max` 50% of the times, in which case the fastest way to keep the distribution uniform is to try again
            } while (value > upperBound);

            return value;
        }
        /// <summary>Generates a random integer between x, such that <paramref name="lowerBound"/> <= x < <paramref name="upperBound"/>
        public static BigInteger Random(BigInteger lowerBound, BigInteger upperBound) {
            if (lowerBound > upperBound) {
                (upperBound, lowerBound) = (lowerBound, upperBound);
            }

            // offset to set min = 0
            BigInteger offset = -lowerBound;
            lowerBound = 0;
            upperBound += offset;

            var value = BigInteger.Random(upperBound) - offset;
            return value;
        }
    }
}
