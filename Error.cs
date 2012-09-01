using System;
using System.Collections.Generic;
using System.Web;
using System.Collections.Specialized;
using System.Web.Script.Serialization;
using StackExchange.Exceptional.Extensions;

namespace StackExchange.Exceptional
{
    /// <summary>
    /// Represents a logical application error (as opposed to the actual exception it may be representing).
    /// </summary>
    [Serializable]
    public class Error
    {
        [ScriptIgnore]
        public long Id { get; set; }

        /// <summary>
        /// Unique identifier for this error, gernated on the server it came from
        /// </summary>
        public Guid GUID { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class.
        /// </summary>
        public Error() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class from a given <see cref="Exception"/> instance.
        /// </summary>
        public Error(Exception e): this(e, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Error"/> class
        /// from a given <see cref="Exception"/> instance and 
        /// <see cref="HttpContext"/> instance representing the HTTP 
        /// context during the exception.
        /// </summary>
        public Error(Exception e, HttpContext context)
        {
            if (e == null) throw new ArgumentNullException("e");

            Exception = e;
            var baseException = e;

            // if it's not a .Net core exception, usually more information is being added
            // so use the wrapper for the message, type, etc.
            // if it's a .Net core exception type, drill down and get the innermost exception
            if (IsBuiltInException(e))
                baseException = e.GetBaseException();

            GUID = Guid.NewGuid();
            ApplicationName = ErrorStore.ApplicationName;
            MachineName = Environment.MachineName;
            Type = baseException.GetType().FullName;
            Message = baseException.Message;
            Source = baseException.Source;
            Detail = e.ToString();
            CreationDate = DateTime.UtcNow;
            DuplicateCount = 1;
            
            var httpException = e as HttpException;
            if (httpException != null)
            {
                StatusCode = httpException.GetHttpCode();
            }

            if (context != null)
            {
                var request = context.Request;
                ServerVariables = new NameValueCollection(request.ServerVariables);
                QueryString = new NameValueCollection(request.QueryString);
                Form = new NameValueCollection(request.Form);
                Cookies = new NameValueCollection(request.Cookies.Count);
                for(var i = 0; i < request.Cookies.Count; i++)
                {
                    Cookies.Add(request.Cookies[i].Name, request.Cookies[i].Value);
                }
            }

            ErrorHash = GetHash();
        }

        /// <summary>
        /// returns if the type of the exception is built into .Net core
        /// </summary>
        /// <param name="e">The exception to check</param>
        /// <returns>True if the exception is a type from within the CLR, false if it's a user/third party type</returns>
        private bool IsBuiltInException(Exception e)
        {
            return e.GetType().Module.ScopeName == "CommonLanguageRuntimeLibrary";
        }

        /// <summary>
        /// Gets a unique-enough hash of this error.  Stored as a quick comparison mehanism to rollup duplicate errors.
        /// </summary>
        /// <returns>"Unique" hash for this error</returns>
        private int? GetHash()
        {
            if (!Detail.HasValue()) return null;

            var result = Detail.GetHashCode();
            if (RollupPerServer && MachineName.HasValue())
                result = (result * 397)^ MachineName.GetHashCode();

            return result;
        }

        /// <summary>
        /// Reflects if the error is protected from deletion
        /// </summary>
        public bool IsProtected { get; set; }

        /// <summary>
        /// Gets the <see cref="Exception"/> instance used to create this error
        /// </summary>
        [ScriptIgnore]
        public Exception Exception { get; set; }

        /// <summary>
        /// Gets the name of the application that threw this exception
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets the hostname of where the exception occured
        /// </summary>
        public string MachineName { get; set; }

        /// <summary>
        /// Get the type of error
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets the source of this error
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Gets the exception message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the detail/stack trace of this error
        /// </summary>
        public string Detail { get; set; }

        /// <summary>
        /// The hash that describes this error
        /// </summary>
        public int? ErrorHash { get; set; }

        /// <summary>
        /// Gets the time in UTC that the error occured
        /// </summary>
        public DateTime CreationDate { get; set; }

        /// <summary>
        /// Gets the HTTP Status code associated with the request
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Gets the server variables collection for the request
        /// </summary>
        [ScriptIgnore]
        public NameValueCollection ServerVariables { get; set; }
        
        /// <summary>
        /// Gets the query string collection for the request
        /// </summary>
        [ScriptIgnore]
        public NameValueCollection QueryString { get; set; }
        
        /// <summary>
        /// Gets the form collection for the request
        /// </summary>
        [ScriptIgnore]
        public NameValueCollection Form { get; set; }
        
        /// <summary>
        /// Gets a collection representing the client cookies of the request
        /// </summary>
        [ScriptIgnore]
        public NameValueCollection Cookies { get; set; }

        /// <summary>
        /// Gets a collection of custom data added at log time
        /// </summary>
        public Dictionary<string, string> CustomData { get; set; }
        
        /// <summary>
        /// The number of newer Errors that have been discarded because they match this Error and fall within the configured 
        /// "IgnoreSimilarExceptionsThreshold" TimeSpan value.
        /// </summary>
        public int? DuplicateCount { get; set; }

        /// <summary>
        /// Gets the SQL command text assocaited with this error
        /// </summary>
        public string SQL { get; set; }
        
        /// <summary>
        /// Date this error was deleted (for stores that support deletion and retention, e.g. SQL)
        /// </summary>
        public DateTime? DeletionDate { get; set; }

        /// <summary>
        /// The URL host of the request causing this error
        /// </summary>
        public string Host { get { return _host ?? (_host = ServerVariables == null ? "" : ServerVariables["HTTP_HOST"]); } set { _host = value; } }
        private string _host;

        /// <summary>
        /// The URL path of the request causing this error
        /// </summary>
        public string Url { get { return _url ?? (_url = ServerVariables == null ? "" : ServerVariables["URL"]); } set { _url = value; } }
        private string _url;

        /// <summary>
        /// The HTTP Method causing this error, e.g. GET or POST
        /// </summary>
        public string HTTPMethod { get { return _httpMethod ?? (_httpMethod = ServerVariables == null ? "" : ServerVariables["REQUEST_METHOD"]); } set { _httpMethod = value; } }
        private string _httpMethod;

        /// <summary>
        /// The IPAddress of the request causing this error
        /// </summary>
        public string IPAddress { get { return _ipAddress ?? (_ipAddress = ServerVariables == null ? "" : ServerVariables.GetRemoteIP()); } set { _ipAddress = value; } }
        private string _ipAddress;
        
        /// <summary>
        /// Json populated from database stored, deserialized after if needed
        /// </summary>
        [ScriptIgnore]
        public string FullJson { get; set; }

        [ScriptIgnore]
        public bool RollupPerServer { get; set; }

        /// <summary>
        /// Returns the value of the <see cref="Message"/> property.
        /// </summary>
        public override string ToString()
        {
            return Message;
        }

        public Error Clone()
        {
            var copy = (Error) MemberwiseClone();
            if (ServerVariables != null) copy.ServerVariables = new NameValueCollection(ServerVariables);
            if (QueryString != null) copy.QueryString = new NameValueCollection(QueryString);
            if (Form != null) copy.Form = new NameValueCollection(Form);
            if (Cookies != null) copy.Cookies = new NameValueCollection(Cookies);
            if (CustomData != null) copy.CustomData = new Dictionary<string, string>(CustomData);
            return copy;
        }

        // Strictly for JSON serialziation, to maintain non-dictonary behavior
        public List<NameValuePair> ServerVariablesSerialzable
        {
            get { return GetPairs(ServerVariables); }
            set { ServerVariables = GetNameValueCollection(value); }
        }
        public List<NameValuePair> QueryStringSerialzable
        {
            get { return GetPairs(QueryString); }
            set { QueryString = GetNameValueCollection(value); }
        }
        public List<NameValuePair> FormSerialzable
        {
            get { return GetPairs(Form); }
            set { Form = GetNameValueCollection(value); }
        }
        public List<NameValuePair> CookiesSerialzable
        {
            get { return GetPairs(Cookies); }
            set { Cookies = GetNameValueCollection(value); }
        }

        public string ToJson()
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(this);
        }

        public string ToDetailedJson()
        {
            var serializer = new JavaScriptSerializer();
            return serializer.Serialize(new
                                            {
                                                GUID,
                                                ApplicationName,
                                                CreationDate = CreationDate.ToEpochTime(),
                                                CustomData,
                                                DeletionDate = DeletionDate.ToEpochTime(),
                                                Detail,
                                                DuplicateCount,
                                                ErrorHash,
                                                HTTPMethod,
                                                Host,
                                                IPAddress,
                                                IsProtected,
                                                MachineName,
                                                Message,
                                                SQL,
                                                Source,
                                                StatusCode,
                                                Type,
                                                Url,
                                                QueryString = ServerVariables["QUERY_STRING"],
                                                ServerVariables = ServerVariablesSerialzable.ToJsonDictionary(),
                                                CookieVariables = CookiesSerialzable.ToJsonDictionary(),
                                                QueryStringVariables = QueryStringSerialzable.ToJsonDictionary(),
                                                FormVariables = FormSerialzable.ToJsonDictionary()
                                            });
        }

        public static Error FromJson(string json)
        {
            var serializer = new JavaScriptSerializer();
            var result = serializer.Deserialize<Error>(json);
            return result;
        }

        public class NameValuePair
        {
            public string Name { get; set; }
            public string Value { get; set; }
        }

        private List<NameValuePair> GetPairs(NameValueCollection nvc)
        {
            var result = new List<NameValuePair>();
            if (nvc == null) return result;

            for (int i = 0; i < nvc.Count; i++)
            {
                result.Add(new NameValuePair {Name = nvc.GetKey(i), Value = nvc.Get(i)});
            }
            return result;
        }

        private NameValueCollection GetNameValueCollection(List<NameValuePair> pairs)
        {
            var result = new NameValueCollection();
            if (pairs == null) return result;

            foreach(var p in pairs)
            {
                result.Add(p.Name, p.Value);
            }
            return result;
        }
    }
}