using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CvarcTool
{
    class Program
    {
        const string LogFolder = "GameLogs/";

        static void Main(string[] args)
        {
            WebInfo.InitWebConfigsFromFile("config&key.txt");
            var results = File.ReadAllLines("Results.txt")
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => l.Split(':'));
            foreach (var result in results)
                SendGameResults(result[0], result[1], int.Parse(result[2]), int.Parse(result[3]), result[4], result[5], result[6]);
        }

        public static void SendGameResults(string leftTag, string rightTag, int leftScore, int rightScore, string logGuid, string type, string subtype)
        {
            if (!CheckForForbiddenSymbols(leftTag) || !CheckForForbiddenSymbols(rightTag))
                return;
            var request = string.Format(
                "http://{0}:{1}/{2}?password={3}&leftTag={4}&rightTag={5}&leftScore={6}&rightScore={7}&logFileName={8}&type={9}&subtype={10}",
                WebInfo.WebIp, WebInfo.WebPort, WebInfo.Method, WebInfo.PasswordToWeb,
                leftTag, rightTag, leftScore, rightScore, logGuid, type, subtype);

            var responseString = SendDirectRequest(request);
            if (responseString != null)
                Console.WriteLine("Game result sent. answer: " + responseString);

            var nvc = new NameValueCollection { { "password", WebInfo.PasswordToWeb } };
            var answer = HttpUploadFile(string.Format(
                "http://{0}:{1}/{2}", WebInfo.WebIp, WebInfo.WebPort, WebInfo.LogMethod),
                LogFolder + logGuid, "file", "multipart/form-data", nvc);

            if (answer != null)
                Console.WriteLine("Game logs sent. answer: " + responseString);

        }

        private static bool CheckForForbiddenSymbols(string value)
        {
            return value.All(ch => char.IsLetterOrDigit(ch) || ch == '-');
        }

        private static string SendDirectRequest(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                var response = request.GetResponse();
                return Encoding.Default.GetString(response.GetResponseStream().ReadToEnd());
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while sending request: " + e.Message);
                return null;
            }
        }

        private static string HttpUploadFile(string url, string file, string paramName, string contentType, NameValueCollection nvc)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
            Stream rs;
            try
            {
                rs = wr.GetRequestStream();
            }
            catch (Exception e)
            {

                Console.WriteLine("Error while sending log file: " + e.Message);
                return null;
            }
            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, file, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                rs.Write(buffer, 0, bytesRead);
            }
            fileStream.Close();

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp;
            try
            {
                wresp = wr.GetResponse();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while sending log file: " + e.Message);
                return null;
            }
            return Encoding.Default.GetString(wresp.GetResponseStream().ReadToEnd());
        }
    }

    public static class HelpExtensions
    {
        public static byte[] ReadToEnd(this Stream stream)
        {
            byte[] buffer = new byte[5];
            MemoryStream memoryStream = new MemoryStream();
            int count;
            do
            {
                count = stream.Read(buffer, 0, 5);
                memoryStream.Write(buffer, 0, count);
            } while (count > 0);
            return memoryStream.ToArray();
        }
    }

    public static class WebInfo
    {
        public static bool NeedToSendToWeb;
        public static string WebIp;
        public static int WebPort;
        public static string Method;
        public static string LogMethod;
        public static string StatusMethod;
        public static string PasswordToWeb; // top defence ever.

        public static void InitWebConfigsFromFile(string pathToConfigFile)
        {
            try
            {
                var lines = File.ReadAllLines(pathToConfigFile);
                var configDict = lines.ToDictionary(line => line.Split(':')[0].Trim(' '),
                    line => line.Split(':')[1].Trim(' '));
                WebIp = configDict["web_ip_or_address"];
                WebPort = int.Parse(configDict["web_port"]);
                Method = configDict["method"];
                LogMethod = configDict["log_method"];
                StatusMethod = configDict["status_method"];
                PasswordToWeb = configDict["secret_to_web"];
                NeedToSendToWeb = bool.Parse(configDict["need_to_communicate_with_web"]);
            }
            catch
            {
                throw new Exception("I need configs, exactly like unity. File not found, or wrong config in file " + pathToConfigFile);
            }
        }
    }
}
