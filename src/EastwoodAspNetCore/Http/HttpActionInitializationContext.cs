namespace Eastwood.Http
{
    /// <summary>
    /// Contains information for the initialization of an <see cref="HttpAction"/>.
    /// </summary>
    public class HttpActionInitializationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpActionInitializationContext"/> class.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        public HttpActionInitializationContext()
        {
            this.ActionOptions = new HttpActionOptions();
            this.Instrumentation = new EmptyInstrumentationContext();
        }
        
        /// <summary>
        /// Gets the action options.
        /// </summary>
        public HttpActionOptions ActionOptions { get; set; }

        /// <summary>
        /// Gets the instrumentation context.
        /// </summary>
        public InstrumentationContext Instrumentation { get; set; }
    }
}