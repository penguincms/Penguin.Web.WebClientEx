using System;
using System.Net;

namespace Penguin.Web
{

    /// <summary>
    /// Webclient with extended functionality stolen from https://stackoverflow.com/questions/1777221/using-cookiecontainer-with-webclient-class
    /// </summary>
    public class WebClientEx : WebClient
    {
        /// <summary>
        /// Creates an instance of this class with an empty cookie container
        /// </summary>
        public WebClientEx()
        {
        }

        /// <summary>
        /// Creates an instance of this class with the provided cookie container
        /// </summary>
        /// <param name="container">The container to use</param>
        public WebClientEx(CookieContainer container)
        {
            this.CookieContainer = container;
        }

        /// <summary>
        /// The cookie container being used for requests
        /// </summary>
        public CookieContainer CookieContainer { get; set; } = new CookieContainer();

        /// <summary>
        /// Overridden to use the cookie container 
        /// </summary>
        /// <param name="address">The address for the request</param>
        /// <returns>A webrequest for the given address that uses the internal cookie container</returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            if (r is HttpWebRequest request)
            {
                request.CookieContainer = CookieContainer;
            }
            return r;
        }

        /// <summary>
        /// Gets a response from the request and stores its cookies in the internal container
        /// </summary>
        /// <param name="request">The original request</param>
        /// <param name="result">I dont know what this is</param>
        /// <returns>The web response</returns>
        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            WebResponse response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        /// <summary>
        /// Gets a response from the request and stores its cookies in the internal container
        /// </summary>
        /// <param name="request">The original request</param>
        /// <returns>The web response</returns>

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        /// <summary>
        /// Reads cookies from a web response and stores them internally
        /// </summary>
        /// <param name="r">The response to read</param>
        private void ReadCookies(WebResponse r)
        {
            if (r is HttpWebResponse response)
            {
                CookieCollection cookies = response.Cookies;
                CookieContainer.Add(cookies);
            }
        }
    }

}
