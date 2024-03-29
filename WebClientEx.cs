﻿using Loxifi;
using Penguin.Extensions.String;
using Penguin.Web.Extensions;
using Penguin.Web.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;

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

        /// <summary>
        /// If true, automatically follows redirects. Defaults to true
        /// </summary>
        public bool FollowRedirect { get; set; } = true;

        /// <summary>
        /// Content type to use for form uploads
        /// </summary>
        public string FormContentType { get; private set; } = "application/x-www-form-urlencoded";

        /// <summary>
        /// Timeout for internal webrequest object
        /// </summary>
        public int TimeOut { get; set; } = -1;

        /// <summary>
        /// User agent applied on requests
        /// </summary>
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
            CookieContainer = container;
        }

        /// <summary>
        /// Loads cookies from a file
        /// </summary>
        /// <param name="path">The path of the file to load</param>
        /// <returns>True if the file exists, otherwise false</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "<Pending>")]
        public bool LoadCookies(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return false;
            }

            CookieContainer = new CookieContainer();

            List<string> cookies = System.IO.File.ReadAllLines(path).ToList();

            foreach (string cookieline in cookies)
            {
                string[] parts = cookieline.Split('\t');

                string name = parts[0];
                string value = parts[1];
                string cpath = parts[2];
                string domain = parts[3];
                string expires = parts[4];

                CookieContainer.Add(new Cookie(name, value, cpath, domain)
                {
                    Expires = DateTime.Parse(expires)
                });
            }

            return true;
        }

        /// <summary>
        /// Saves the underlying client cookies to a given path for future use
        /// </summary>
        /// <param name="path">The path to save the cookies</param>
        public void SaveCookies(string path)
        {
            List<string> output = new();

            foreach (Cookie c in CookieContainer.GetAllCookies())
            {
                output.Add($"{c.Name}\t{c.Value}\t{c.Path}\t{c.Domain}\t{c.Expires}");
            }

            System.IO.File.WriteAllLines(path, output);
        }

        /// <summary>
        /// Allows adding of cookies to container using host and HTTP header string
        /// </summary>
        /// <param name="CookieHeader"></param>
        /// <param name="host"></param>
        public void SetCookiesFromHeader(string CookieHeader, string host)
        {
            CookieContainer.SetCookies(new Uri(host), CookieHeader);
        }

        /// <summary>
        /// Nofail download data with result details
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public WebClientExResponse<byte[]> TryDownloadData(string url)
        {
            return TryGet(() => DownloadData(url));
        }

        /// <summary>
        /// Nofail download string with result details
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public WebClientExResponse<string> TryDownloadString(string url)
        {
            return TryGet(() => DownloadString(url));
        }

        /// <summary>
        /// Uploads a dictionary as a form post, and sets the proper headers
        /// </summary>
        /// <param name="url">The Url to post to</param>
        /// <param name="postData">The data to post in the body</param>
        /// <returns>body response from server</returns>
        public string UploadForm(string url, Dictionary<string, string> postData)
        {
            string postDataStr = string.Join("&", postData.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value).Replace("!", "%21")}"));

            Headers.Add("Content-Type", FormContentType);

            int tries = 0;

            do
            {
                try
                {
                    return UploadString(url, postDataStr);
                }
                catch (WebException wex) when (tries++ < 3 && wex.Message.Contains("(502)"))
                {
                    Debug.WriteLine(wex.Message);
                }
            } while (true);
        }

        /// <summary>
        /// Posts an Http Query object as a form
        /// </summary>
        /// <param name="url">The url to post to</param>
        /// <param name="query">The object to post</param>
        /// <returns>the response string from the server</returns>
        public virtual string UploadHttpQuery(string url, HttpQuery query)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException($"'{nameof(url)}' cannot be null or whitespace.", nameof(url));
            }

            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            Headers[HttpRequestHeader.ContentType] = FormContentType;

            return UploadString(url, query.ToString());
        }

        /// <summary>
        /// Overridden to use the cookie container
        /// </summary>
        /// <param name="address">The address for the request</param>
        /// <returns>A webrequest for the given address that uses the internal cookie container</returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            if (!string.IsNullOrWhiteSpace(UserAgent) && !Headers.AllKeys.Any(h => h.Equals("user-agent", StringComparison.OrdinalIgnoreCase)))
            {
                Headers.Add("user-agent", UserAgent);
            }

            WebRequest r = base.GetWebRequest(address);

            if (TimeOut != -1)
            {
                r.Timeout = TimeOut;
            }

            if (r is HttpWebRequest request)
            {
                if (!FollowRedirect)
                {
                    request.AllowAutoRedirect = false;
                }

                request.CookieContainer = CookieContainer;
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
            ReadCookies(response);
            return response;
        }

        /// <summary>
        /// Overrides base to support configurable redirect handling and cookies
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response;
            try
            {
                response = base.GetWebResponse(request);
            }
            catch (WebException wex) when (wex.Response is HttpWebResponse wexresponse && (int)wexresponse.StatusCode >= 300 && (int)wexresponse.StatusCode < 400)
            {
                response = wexresponse;
            }

            ReadCookies(response);
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

            Cookie c = new(Name, Value, Path, Domain);

            if (expires != DateTime.MinValue)
            {
                c.Expires = expires;
            }

            return c;
        }

        private static WebClientExResponse<T> TryGet<T>(Func<T> func)
        {
            WebClientExResponse<T> result = new();

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
            HashSet<string> readNames = new();

            if (r is HttpWebResponse response)
            {
                CookieCollection cookies = response.Cookies;

                foreach (Cookie c in cookies)
                {
                    _ = readNames.Add(c.Name);
                }

                CookieContainer.Add(cookies);
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
                        CookieContainer.Add(c);
                        _ = readNames.Add(c.Name);
                    }
                }
            }
        }
    }
}