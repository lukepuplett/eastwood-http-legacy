using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eastwood.Mapping;
using Eastwood.Binary;

namespace Eastwood.Http
{
    public static class ETagUtility
    {
        public static bool TryCreate(IEnumerable<byte[]> rowVersions, out byte[] eTag)
        {
            if (rowVersions == null)
            {
                throw new ArgumentNullException(nameof(rowVersions));
            }


            if (rowVersions.Any() && rowVersions.All(i => i != null))
            {
                BitArray bits = new BitArray(rowVersions.First());

                foreach (var y in rowVersions.Skip(1))
                {
                    bits = bits.Xor(new BitArray(y));
                }

                eTag = BitHelper.ConvertToBytes(bits);
                return true;
            }
            else
            {
                eTag = null;
                return false;
            }
        }

        public static bool TryCreate(DateTimeOffset timeStamp, out object eTagBytes)
        {
            throw new NotImplementedException();
        }

        public static bool TryCreate(IEnumerable<IRowVersion> versionedItems, out byte[] eTag)
        {
            if (versionedItems == null)
            {
                throw new ArgumentNullException(nameof(versionedItems));
            }


            if (versionedItems.Any())
            {
                return TryCreate(versionedItems.Select(i => i.RowVersion), out eTag);
            }
            else
            {
                eTag = null;
                return false;
            }
        }

        public static bool TryCreate(IEnumerable<IModifiedTimestamp> modifiableItems, out string eTag)
        {
            if (modifiableItems == null)
            {
                throw new ArgumentNullException(nameof(modifiableItems));
            }


            if (modifiableItems.Any())
            {
                // In a collection, only the most recent modified date is interesting.

                eTag = BitHelper.ConvertToHex(modifiableItems.Max(i => i.ModifiedOn));
                return true;
            }
            else
            {
                eTag = null;
                return false;
            }
        }

        public static bool TryCreate(IModifiedTimestamp modified, out string eTag)
        {
            if (modified == null)
                throw new ArgumentNullException("modified");


            eTag = null;
            if (modified != null)
            {
                return TryCreate(modified.ModifiedOn, out eTag);
            }

            return false;
        }

        public static bool TryCreate(DateTimeOffset timestamp, out string eTag)
        {
            eTag = BitHelper.ConvertToHex(timestamp);
            return true;
        }

        public static bool TryCreate(DateTimeOffset timestamp, out byte[] eTag)
        {
            eTag = BitHelper.ConvertToBytes(timestamp);
            return true;
        }

        public static bool TryCreate(byte[] rowVersion, out string eTag)
        {
            if (rowVersion == null)
                throw new ArgumentNullException("rowVersion");


            eTag = null;
            if (rowVersion != null)
            {
                eTag = BitHelper.ConvertToHex(rowVersion);
            }
            return eTag != null;
        }

        public static bool TryCreate(IRowVersion rowVersion, out string eTag)
        {
            if (rowVersion == null)
                throw new ArgumentNullException("rowVersion");


            eTag = null;
            if (rowVersion != null)
            {
                eTag = BitHelper.ConvertToHex(rowVersion.RowVersion);
            }
            return eTag != null;
        }

        public static bool TryCreate(IPreconditionInformation info, out string eTag)
        {
            if (info == null)
                throw new ArgumentNullException("info");


            eTag = null;
            if (info.RowVersion != null && info.RowVersion.Length > 0)
            {
                eTag = BitHelper.ConvertToHex(info.RowVersion);
            }
            else
            {
                eTag = BitHelper.ConvertToHex(info.ModifiedOn);
            }
            return eTag != null;
        }

        /// <summary>
        /// Tries to convert a hexadecimal or base-64 ETag to bytes.
        /// </summary>
        /// <param name="tagString">The tag string suspected to be hex or base-64.</param>
        /// <param name="bytes">The bytes.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool TryConvertToBytes(string tagString, out byte[] bytes)
        {
            bytes = null;

            if (!String.IsNullOrWhiteSpace(tagString))
            {
                try
                {
                    if (tagString.Length % 2 == 0) // Is even.
                    {
                        int c = 0;
                        bytes = new byte[tagString.Length / 2];

                        for (int i = 0; i < tagString.Length; i += 2)
                        {
                            bytes[c++] = Convert.ToByte(tagString.Substring(i, 2), 16);
                        }
                    }
                }
                catch (FormatException)
                {
                    // Not hex. Maybe its base-64.

                    bytes = null;

                    try
                    {
                        bytes = Convert.FromBase64String(tagString);
                    }
                    catch (FormatException)
                    {
                        // Not base-64 either.
                    }
                }
            }

            return bytes != null;
        }

        public static string FormatStandard(string eTag)
        {
            if (String.IsNullOrWhiteSpace(eTag))
                throw new ArgumentException("The string argument is null or whitespace.", "eTag");

            return String.Concat("\"", eTag, "\"");
        }
    }
}
