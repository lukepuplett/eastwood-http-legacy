using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eastwood.Mapping;

namespace Eastwood.Http
{
    /// <summary>
    /// Represents the aggregate of a collection of precondition information.
    /// </summary>
    /// <remarks>
    /// This class is used to create versioning data from a set of versionable items.
    /// </remarks>
    public sealed class AggregatePreconditionInformation : IPreconditionInformation
    {
        /// <summary>
        /// Gets or sets the modified on.
        /// </summary>
        /// <value>
        /// The modified on.
        /// </value>
        public DateTimeOffset ModifiedOn { get; set; }

        /// <summary>
        /// Gets or sets the row version.
        /// </summary>
        /// <value>
        /// The row version.
        /// </value>
        public byte[] RowVersion { get; set; }

        /// <summary>
        /// Creates aggregated precondition information from a collection of precondition information.
        /// </summary>
        /// <param name="items">The items.</param>        
        /// <exception cref="System.ArgumentNullException">items</exception>
        public static AggregatePreconditionInformation CreateFrom(IEnumerable<IPreconditionInformation> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");


            var info = new AggregatePreconditionInformation();

            // Set the etag.

            string eTag;
            var rowVersions = items.Select(i => i.RowVersion).ToArray();

            if (ETagUtility.TryCreate(rowVersions, out eTag))
            {
                info.RowVersion = Hex.ToByteArray(eTag);
            }

            // Set the modified.

            if (items.Any())
                info.ModifiedOn = items.Max(i => i.ModifiedOn);


            return info;
        }

        /// <summary>
        /// Creates aggregated precondition information from a collection of versioned items.
        /// </summary>
        /// <param name="items">The items.</param>        
        /// <exception cref="System.ArgumentNullException">items</exception>
        public static AggregatePreconditionInformation CreateFrom(IEnumerable<IEntityTag> items)
        {
            if (items == null)
                throw new ArgumentNullException("items");


            IPreconditionInformation[] pItems = items.Select(i => new AggregatePreconditionInformation()
            {
                RowVersion = Hex.ToByteArray(i.ETag),
                ModifiedOn = GetModifiedOn(i)

            }).ToArray();

            return CreateFrom(pItems);
        }

        /// <summary>
        /// Creates aggregated precondition information from a collection of timestamps.
        /// </summary>
        /// <param name="timeStamps">The time stamps.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">timeStamps</exception>
        /// <exception cref="System.InvalidOperationException">Cannot create precondition information from the timestamps. This is unexpected as it is assumed that it is always possible.</exception>
        public static AggregatePreconditionInformation CreateFrom(IEnumerable<DateTimeOffset> timeStamps)
        {
            if (timeStamps == null)
                throw new ArgumentNullException("timeStamps");


            DateTimeOffset mostRecent = timeStamps.DefaultIfEmpty().Max();

            string eTag;
            if (ETagUtility.TryCreate(mostRecent, out eTag))
            {
                return new AggregatePreconditionInformation()
                {
                    RowVersion = Hex.ToByteArray(eTag),
                    ModifiedOn = mostRecent
                };
            }
            else
            {
                throw new InvalidOperationException(
                    "Cannot create precondition information from the timestamps. This is unexpected as it is assumed that it is always possible.");
            }
        }

        private static DateTimeOffset GetModifiedOn(IEntityTag item)
        {
            IModifiedTimestamp m = item as IModifiedTimestamp;
            if (m == null)
                return DateTimeOffset.MinValue;

            return m.ModifiedOn;
        }
    }
}
