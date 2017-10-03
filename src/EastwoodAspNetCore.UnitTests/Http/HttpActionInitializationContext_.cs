using Xunit;

namespace Eastwood.Http
{
    public class HttpActionInitializationContext_
    {
        [Fact]
        public void Ctor__initializes_properly()
        {
            HttpActionInitializationContext context = new HttpActionInitializationContext();
            
            Assert.NotNull(context.ActionOptions);
            Assert.NotNull(context.Instrumentation);
        }
    }
}
