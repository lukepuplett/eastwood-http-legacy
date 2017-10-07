using System;
using System.Collections;
using System.Numerics;
using System.Text;

namespace Eastwood.Binary
{
    /// <summary>
    /// A true grafter in the library, this class generally helps out and mucks in with bit wrangling duties.
    /// </summary>
    internal static class BitHelper
    {
        /// <summary>
        /// Converts a BitArray to a BigInteger.
        /// </summary>
        /// <param name="bits">The bits.</param>
        /// <returns>BigInteger.</returns>
        /// <exception cref="ArgumentNullException">bits</exception>
        public static BigInteger ConvertToBigInteger(BitArray bits)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }


            byte[] buf = ConvertToBytes(bits);

            // BigInteger is a signed data structure and so the last bit must be zero. We must ensure that a byte 
            // of value 0 is appended, so 255, 255 becomes 255, 255, 0 or 11111111 11111111 00000000.

            // Otherwise a value of 11111111 11111111 is -1. When BigInteger has a value of 127 that is 11111110
            // which fits in 7 bits with the last as the sign. So 128 then needs two bytes to store it.

            Array.Resize(ref buf, buf.Length + 1);

            return new BigInteger(buf);
        }

        /// <summary>
        /// Returns all the bits as a byte array.
        /// </summary>
        /// <returns>An array of bytes</returns>
        public static byte[] ConvertToBytes(BitArray array)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }


            byte[] ret = new byte[(array.Length - 1) / 8 + 1];
            array.CopyTo(ret, 0);
            return ret;
        }

        /// <summary>
        /// Turns a byte array into its Hex representation.
        /// </summary>
        public static string ConvertToHex(byte[] instance)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in instance)
            {
                sb.Append(b.ToString("X").PadLeft(2, "0"[0]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts a BitArray to hexadecimal.
        /// </summary>
        /// <param name="bits">The bits.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="ArgumentNullException">bits</exception>
        public static string ConvertToHex(BitArray bits)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }

            return ConvertToHex(ConvertToBytes(bits));
        }

        /// <summary>
        /// Converts a DateTimeOffset to a hexadecimal representation.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>System.String.</returns>
        public static string ConvertToHex(DateTimeOffset timestamp)
        {
            return ConvertToHex(ConvertToBytes(timestamp));
        }

        /// <summary>
        /// Converts a DateTimeOffset to a byte array.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>System.Byte[].</returns>
        public static byte[] ConvertToBytes(DateTimeOffset timestamp)
        {
            return BitConverter.GetBytes(timestamp.ToUnixTimeMilliseconds());
        }
    }
}
