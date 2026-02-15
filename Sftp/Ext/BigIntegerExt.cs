// BigIntegerExt.cs - Part of the ZipZap project for storing files online
//     Copyright (C) 2026  Barenboim Esther wadsaek@gmail.com
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY, without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <https://www.gnu.org/licenses/>.

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
