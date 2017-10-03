using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using System.Collections.Generic;
using System.Text;
using Xunit;
using System;

namespace Eastwood.Http
{
    public partial class HttpPreconditionExecutor_
    {
        class ServerSideEntity : IPreconditionInformation
        {
            public byte[] RowVersion { get; set; }

            public DateTimeOffset ModifiedOn { get; set; }
        }

        //

        [Fact]
        public void Ctor__when__logger_factory_is_null__then__it_still_works()
        {
            var executor = new HttpPreconditionExecutor(null);

            Assert.NotNull(executor);
        }

        //

        [Fact]
        public void GetResultForMutating__when__GET_IfNoneMatch_and_row_has_no_version_info_and_default_BadRequest__then__precondition_BadRequest()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-None-Match", ("abc123") }
                })
            };

            var rowWithNoVersionInfo = new ServerSideEntity();

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithNoVersionInfo);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Indeterminable, result.Status);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__GET_IfNoneMatch_and_row_has_only_time_version_info_and_default_Pass__then__precondition_passes()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-None-Match", ("abc123") }
                })
            };

            var rowWithJustModifiedStamp = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now.AddMinutes(-10)
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.PreconditionPassed, rowWithJustModifiedStamp);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Passed, result.Status);
            Assert.Equal(0, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__GET_IfNoneMatch_and_row_has_only_time_version_info_and_default_BadRequest__then__precondition_BadRequest()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-None-Match", ("abc123") }
                })
            };

            var rowWithJustModifiedStamp = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now.AddMinutes(-10)
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithJustModifiedStamp);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Indeterminable, result.Status);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__GET_IfModifiedSince_and_row_was_not_modified__then__precondition_NotModified()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Modified-Since", "Wed, 21 Oct 2015 07:28:00 GMT" }
                })
            };

            var rowModifiedYearsAgo = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Parse("26 Oct 1985 09:00:00 GMT")
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowModifiedYearsAgo);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Failed, result.Status);
            Assert.Equal(StatusCodes.Status304NotModified, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__GET_IfModifiedSince_and_row_was_modified_recently__then__precondition_Pass()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Modified-Since", "Wed, 21 Oct 2015 07:28:00 GMT" }
                })
            };

            var rowModifiedJustNow = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowModifiedJustNow);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Passed, result.Status);
            Assert.Equal(0, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__GET_IfNoneMatch_and_row_has_matching_ETag__then__precondition_NotModified()
        {
            byte[] rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            string eTag;
            Assert.True(ETagUtility.TryCreate(rowVersion, out eTag));

            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Get,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-None-Match", (ETagUtility.FormatStandard(eTag)) }
                })
            };

            var rowWithMatchingETag = new ServerSideEntity()
            {
                RowVersion = rowVersion
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithMatchingETag);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Failed, result.Status);
            Assert.Equal(StatusCodes.Status304NotModified, result.StatusCode);
        }

        //

        [Fact]
        public void GetResultForMutating__when__PUT_IfMatch_and_row_has_no_version_info_and_default_BadRequest__then__precondition_BadRequest()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Match", ("abc123") }
                })
            };

            var rowWithNoVersionInfo = new ServerSideEntity();

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithNoVersionInfo);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Indeterminable, result.Status);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__PUT_IfMatch_and_row_has_only_time_version_info_and_default_Pass__then__precondition_passes()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Match", ("abc123") }
                })
            };

            var rowWithJustModifiedStamp = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now.AddMinutes(-10)
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.PreconditionPassed, rowWithJustModifiedStamp);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Passed, result.Status);
            Assert.Equal(0, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__PUT_IfMatch_and_row_has_only_time_version_info_and_default_BadRequest__then__precondition_BadRequest()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Match", ("abc123") }
                })
            };

            var rowWithJustModifiedStamp = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now.AddMinutes(-10)
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithJustModifiedStamp);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Indeterminable, result.Status);
            Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__PUT_IfUnmodifiedSince_and_row_was_not_modified__then__precondition_Pass()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Unmodified-Since", "Wed, 21 Oct 2015 07:28:00 GMT" }
                })
            };

            var rowModifiedYearsAgo = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Parse("Wed, 21 Oct 2015 07:28:00 GMT") // Must match. The client must have the exact timestamp of the server copy.
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowModifiedYearsAgo);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Passed, result.Status);
            Assert.Equal(0, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__PUT_IfUnmodifiedSince_and_row_was_modified_recently__then__precondition_Fail()
        {
            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Unmodified-Since", "Wed, 21 Oct 2015 07:28:00 GMT" }
                })
            };

            var rowModifiedJustNow = new ServerSideEntity()
            {
                ModifiedOn = DateTimeOffset.Now
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowModifiedJustNow);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Failed, result.Status);
            Assert.Equal(StatusCodes.Status412PreconditionFailed, result.StatusCode);
        }

        [Fact]
        public void GetResultForMutating__when__PUT_IfMatch_and_row_has_matching_ETag__then__precondition_Pass()
        {
            byte[] rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            string eTag;
            Assert.True(ETagUtility.TryCreate(rowVersion, out eTag));

            var request = new FakeHttpRequest()
            {
                Method = HttpMethods.Put,
                FakeHeaders = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>()
                {
                    { "If-Match", (ETagUtility.FormatStandard(eTag)) }
                })
            };

            var rowWithMatchingETag = new ServerSideEntity()
            {
                RowVersion = rowVersion
            };

            var executor = new HttpPreconditionExecutor(null);

            // Act

            var result = executor.GetResult(request, PreconditionResult.BadRequest, rowWithMatchingETag);

            // Assert

            Assert.NotNull(result);
            Assert.Equal(PreconditionResultStatus.Passed, result.Status);
            Assert.Equal(0, result.StatusCode);
        }

        [Fact]
        public void __when__x__then__y()
        {

        }
    }
}
