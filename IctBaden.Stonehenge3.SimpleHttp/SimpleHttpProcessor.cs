using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace IctBaden.Stonehenge3.SimpleHttp
{
    internal class SimpleHttpProcessor
    {
        private readonly TcpClient _socket;
        private readonly SimpleHttpServer _server;

        private Stream _inputStream;
        private StreamWriter _outputStream;

        public string Method { get; private set; }
        public string Url { get; private set; }
        public string Query { get; private set; }
        public string ProtocolVersion { get; private set; }
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();


        private const int MaxPostSize = 10 * 1024 * 1024; // 10MB

        public SimpleHttpProcessor(TcpClient clientSocket, SimpleHttpServer httpServer)
        {
            _socket = clientSocket;
            _server = httpServer;
        }

        public void Process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            _inputStream = new BufferedStream(_socket.GetStream());

            // we probably shouldn't be using a stream writer for all output from handlers either
            _outputStream = new StreamWriter(new BufferedStream(_socket.GetStream()), new UTF8Encoding(false)) { NewLine = "\r\n" };
            try
            {
                ParseRequest();
                ReadHeaders();
                if (Method.Equals("GET"))
                {
                    HandleGetRequest();
                }
                else if (Method.Equals("POST"))
                {
                    HandlePostRequest();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception: " + ex);
                WriteNotFound();
            }
            try
            {
                _outputStream.Flush();
                _socket.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            _inputStream.Dispose();
            _inputStream = null;
            _outputStream = null;
        }

        private string ReadInputLine()
        {
            var line = string.Empty;
            for(var wait = 0; wait < 1000; wait++)
            {
                var nextChar = _inputStream.ReadByte();
                switch (nextChar)
                {
                    case '\n':
                        return line;
                    case '\r':
                        break;
                    case -1:
                        Thread.Sleep(1);
                        break;
                    default:
                        line += Convert.ToChar(nextChar);
                        break;
                }
            }
            return line;
        }

        private void ParseRequest()
        {
            var request = ReadInputLine();
            if (request == null)
            {
                throw new Exception("invalid http request line");
            }
            Debug.WriteLine("request=" + request);
            var tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            Method = tokens[0].ToUpper();
            Url = HttpUtility.UrlDecode(tokens[1]);
            Query = string.Empty;
            ProtocolVersion = tokens[2];

            if (Url?.Contains('?') ?? false)
            {
                tokens = Url.Split('?');
                Url = tokens[0];
                Query = tokens[1];
            }

            Debug.WriteLine("starting: " + request);
        }

        private void ReadHeaders()
        {
            Debug.WriteLine("ReadHeaders()");
            string line;
            while ((line = ReadInputLine()) != null)
            {
                Debug.WriteLine("header line=" + line);
                if (line.Equals(""))
                {
                    Debug.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Debug.WriteLine("header: {0}:{1}", name, value);
                Headers[name] = value;
            }
        }

        public void HandleGetRequest()
        {
            _server.HandleGetRequest(this);
        }

        private const int BufSize = 4096;
        public void HandlePostRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Debug.WriteLine("get post data start");
            var contentStream = new MemoryStream();
            if (Headers.ContainsKey("Content-Length"))
            {
                var contentLen = Convert.ToInt32(Headers["Content-Length"]);
                if (contentLen > MaxPostSize)
                {
                    throw new Exception($"POST Content-Length({contentLen}) too big for this simple server");
                }
                var buf = new byte[BufSize];
                var toRead = contentLen;
                while (toRead > 0)
                {
                    Debug.WriteLine("starting Read, toRead={0}", toRead);
                    var numRead = _inputStream.Read(buf, 0, Math.Min(BufSize, toRead));
                    Debug.WriteLine("read finished, numRead={0}", numRead);
                    if (numRead == 0)
                    {
                        if (toRead == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    toRead -= numRead;
                    contentStream.Write(buf, 0, numRead);
                }
                contentStream.Seek(0, SeekOrigin.Begin);
            }
            Debug.WriteLine("get post data end");
            _server.HandlePostRequest(this, new StreamReader(contentStream));
        }

        public void WriteSuccess(string contentType = "text/html", Dictionary<string,string> header = null)
        {
            if (header == null)
                header = new Dictionary<string, string>();
            if (!header.ContainsKey("Content-Type"))
                header.Add("Content-Type", contentType);
            if (!header.ContainsKey("Connection"))
                header.Add("Connection", "close");

            WriteHeader(HttpStatusCode.OK, header);
        }

        public void WriteNotFound()
        {
            var header = new Dictionary<string, string> { { "Connection", "close" } };
            WriteHeader(HttpStatusCode.NotFound, header);
        }

        public void WriteRedirect(string redirectionUrl, Dictionary<string, string> header = null)
        {
            if (header == null)
                header = new Dictionary<string, string>();

            if (!header.ContainsKey("Location"))
                header.Add("Location", redirectionUrl);
            if (!header.ContainsKey("Connection"))
                header.Add("Connection", "close");

            WriteHeader(HttpStatusCode.Redirect, header);
        }

        public void WriteHeader(HttpStatusCode code, Dictionary<string, string> header)
        {
            _outputStream.WriteLine($"HTTP/1.0 {(int)code} {code}");

            var headers = header.Select(h => $"{h.Key}: {h.Value}");
            _outputStream.WriteLine(string.Join(Environment.NewLine, headers));

            _outputStream.WriteLine(""); // this terminates the HTTP headers.
        }

        public void WriteContent(byte[] data)
        {
            _outputStream.Flush();
            _outputStream.BaseStream.WriteAsync(data, 0, data.Length);
        }

        public void WriteContent(string text)
        {
            _outputStream.WriteAsync(text);
        }
    }
}