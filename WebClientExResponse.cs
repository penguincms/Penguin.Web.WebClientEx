using System;
using System.Net;

namespace Penguin.Web
{
    public class WebClientExResponse<T>
    {
        public Exception Exception { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; }
        public T Result { get; set; }
        public bool Success { get; set; }
    }
}