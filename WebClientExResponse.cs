using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Penguin.Web
{
    public class WebClientExResponse<T>
    {
        public bool Success { get; set; }
        public HttpStatusCode HttpStatusCode { get; set; }
        public Exception Exception { get; set; }

        public T Result { get; set; }
    }
}
