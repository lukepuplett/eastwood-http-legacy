using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eastwood.Mapping;

namespace Eastwood.Http
{
    public static class ETagUtility
    {
        public static bool TryCreate(IEnumerable<byte[]> rowVersions, out string eTag)
        {
            if (rowVersions == null)
                throw new ArgumentNullException("rowVersions");


            if (rowVersions.Any() && rowVersions.All(i => i != null))
            {
                BitArray bits = new BitArray(rowVersions.First());

                foreach (var y in rowVersions.Skip(1))
                {
                    bits = bits.Xor(new BitArray(y));
                }

                eTag = bits.ToByteArray().ToHex();
                return true;
            }
            else
            {
                eTag = null;
                return false;
            }
        }

        public static bool TryCreate(IEnumerable<IRowVersion> versionedItems, out string eTag)
        {
            if (versionedItems == null)
                throw new ArgumentNullException("versionedItems");


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
                throw new ArgumentNullException("modifiableItems");


            if (modifiableItems.Any())
            {
                // In a collection, only the most recent modified date is interesting.

                eTag = modifiableItems.Max(i => i.ModifiedOn).ToISO8601String().ToUTF8ByteArray().ToHex();
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
            eTag = timestamp.ToISO8601String().ToUTF8ByteArray().ToHex();
            return true;
        }

        public static bool TryCreate(IRowVersion rowVersion, out string eTag)
        {
            if (rowVersion == null)
                throw new ArgumentNullException("rowVersion");


            eTag = null;
            if (rowVersion != null)
            {
                eTag = rowVersion.RowVersion.ToHex();
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
                eTag = info.RowVersion.ToHex();
            }
            else
            {
                eTag = info.ModifiedOn.ToISO8601String().ToUTF8ByteArray().ToHex();
            }
            return eTag != null;
        }

        public static bool TryReadBytes(string tagString, out byte[] bytes)
        {
            bytes = null;

            if (!String.IsNullOrWhiteSpace(tagString))
            {
                if (tagString.Length.IsEven())
                {
                    int c = 0;
                    bytes = new byte[tagString.Length / 2];

                    for (int i = 0; i < tagString.Length; i += 2)
                    {
                        try
                        {
                            bytes[c++] = Convert.ToByte(tagString.Substring(i, 2), 16);
                        }
                        catch (FormatException)
                        {
                            // Not hex.

                            bytes = null;

                            try
                            {
                                bytes = Convert.FromBase64String(tagString);
                            }
                            catch (FormatException)
                            {
                                // Not base-64.
                            }
                        }
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
