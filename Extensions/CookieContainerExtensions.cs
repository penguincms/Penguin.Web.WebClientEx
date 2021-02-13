using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

namespace Penguin.Web.Extensions
{
    public static class CookieContainerExtensions
    {
        /// <summary>
        /// This method will ensure to get all cookies, no matter what the protocol is
        /// https://stackoverflow.com/questions/15983166/how-can-i-get-all-cookies-of-a-cookiecontainer
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static IEnumerable<Cookie> GetAllCookies(this CookieContainer c)
        {
            if (c is null)
            {
                throw new System.ArgumentNullException(nameof(c));
            }

            Hashtable k = (Hashtable)c.GetType().GetField("m_domainTable", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c);
            foreach (DictionaryEntry element in k)
            {
                SortedList l = (SortedList)element.Value.GetType().GetField("m_list", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(element.Value);
                foreach (object e in l)
                {
                    CookieCollection cl = (CookieCollection)((DictionaryEntry)e).Value;
                    foreach (Cookie fc in cl)
                    {
                        yield return fc;
                    }
                }
            }
        }
    }
}