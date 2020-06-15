using Penguin.Extensions.Strings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Penguin.Web
{
    /// <summary>
    /// Webclient with extended functionality stolen from https://stackoverflow.com/questions/1777221/using-cookiecontainer-with-webclient-class
    /// </summary>
    public class WebClientEx : WebClient
    {
        /// <summary>
        /// The cookie container being used for requests
        /// </summary>
        public CookieContainer CookieContainer { get; set; } = new CookieContainer();

        public bool FollowRedirect { get; set; } = true;
        public string UserAgent { get; set; }

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

        public void SetCookiesFromHeader(string CookieHeader, string host)
        {
            CookieContainer.SetCookies(new Uri(host), CookieHeader);
        }

        public WebClientExResponse<byte[]> TryDownloadData(string url)
        {
            return TryGet(() => { return DownloadData(url); });
        }

        public WebClientExResponse<string> TryDownloadString(string url)
        {
            return TryGet(() => { return DownloadString(url); });
        }

        /// <summary>
        /// Overridden to use the cookie container
        /// </summary>
        /// <param name="address">The address for the request</param>
        /// <returns>A webrequest for the given address that uses the internal cookie container</returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            if (!string.IsNullOrWhiteSpace(UserAgent) && !this.Headers.AllKeys.Any(h => h.Equals("user-agent", StringComparison.OrdinalIgnoreCase)))
            {
                this.Headers.Add("user-agent", UserAgent);
            }

            WebRequest r = base.GetWebRequest(address);

            if (r is HttpWebRequest request)
            {
                if (!FollowRedirect)
                {
                    request.AllowAutoRedirect = false;
                }

                request.CookieContainer = this.CookieContainer;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
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
            this.ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response;
            try
            {
                response = base.GetWebResponse(request);
            }
            catch (WebException wex) when (!FollowRedirect && wex.Response is HttpWebResponse wexresponse && (int)wexresponse.StatusCode >= 300 && (int)wexresponse.StatusCode < 400)
            {
                response = wexresponse;
            }

            this.ReadCookies(response);
            return response;
        }

        private static Cookie SplitCookie(string cookieString, string host = null)
        {
            string Name = string.Empty;
            string Value = string.Empty;
            string Path = string.Empty;
            string Domain = string.Empty;
            DateTime expires = DateTime.MinValue;

            for (int i = 0; i < cookieString.Split(';').Length; i++)
            {
                string part = cookieString.Split(';')[i].Trim();

                if (i == 0)
                {
                    Name = part.To("=").Trim();
                    Value = part.From("=").Trim();
                }
                else
                {
                    if (part.StartsWith("path=", StringComparison.OrdinalIgnoreCase))
                    {
                        Path = part.From("=").Trim();
                    }

                    if (part.StartsWith("Expires=", StringComparison.OrdinalIgnoreCase))
                    {
                        expires = DateTime.Parse(part.From("="));
                    }

                    if (part.StartsWith("Domain=", StringComparison.OrdinalIgnoreCase))
                    {
                        Domain = part.From("=").Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(Domain))
            {
                Domain = host;
            }

            Cookie c = new Cookie(Name, Value, Path, Domain);

            if (expires != DateTime.MinValue)
            {
                c.Expires = expires;
            }

            return c;
        }

        /// <summary>
        /// Gets a response from the request and stores its cookies in the internal container
        /// </summary>
        /// <returns>The web response</returns>
        /// <summary>
        /// Reads cookies from a web response and stores them internally
        /// </summary>
        /// <param name="r">The response to read</param>
        private void ReadCookies(WebResponse r)
        {
            HashSet<string> readNames = new HashSet<string>();

            if (r is HttpWebResponse response)
            {
                CookieCollection cookies = response.Cookies;

                foreach (Cookie c in cookies)
                {
                    readNames.Add(c.Name);
                }

                this.CookieContainer.Add(cookies);
            }

            string setHeader = r.Headers["Set-Cookie"];

            if (!string.IsNullOrWhiteSpace(setHeader))
            {
                setHeader = Regex.Replace(setHeader, "((?i)(Expires)=(?i)[a-z]{3}),", "$1");

                foreach (string cookie in setHeader.Split(","))
                {
                    Cookie c = SplitCookie(cookie, r.ResponseUri.Host);

                    if (!readNames.Contains(c.Name))
                    {
                        this.CookieContainer.Add(c);
                        readNames.Add(c.Name);
                    }
                }
            }
        }

        private WebClientExResponse<T> TryGet<T>(Func<T> func)
        {
            WebClientExResponse<T> result = new WebClientExResponse<T>();

            try
            {
                result.Result = func.Invoke();

                result.Success = true;

                return result;
            }
            catch (Exception ex)
            {
                result.Exception = ex;

                result.HttpStatusCode = HttpStatusCode.BadRequest;

                if (ex is WebException wex && wex.Response is HttpWebResponse response)
                {
                    result.HttpStatusCode = response.StatusCode;
                }
            }

            return result;
        }
    }
}