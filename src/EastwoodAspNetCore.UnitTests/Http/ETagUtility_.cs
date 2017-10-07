using Eastwood.Binary;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Eastwood.Http
{
    public class ETagUtility_
    {
        private static DateTimeOffset TimeStamp = new DateTimeOffset(2017, 09, 29, 14, 32, 10, TimeSpan.Zero);

        [Fact]
        public void TryCreate__when__static_test_date__then__produces_10460DCE5E010000()
        {
            string eTagString;
            Assert.True(ETagUtility.TryCreate(TimeStamp, out eTagString));
            Assert.Equal("10460DCE5E010000", eTagString);
        }
    }
}
