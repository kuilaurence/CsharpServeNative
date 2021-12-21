using System;
using LitJson;
using System.IO;
using System.Net;
using System.Data;
using System.Threading;
using System.Net.Sockets;
using System.Collections;

namespace Bend.Util
{
    public class HttpProcessor
    {
        public string http_url;
        public string http_method;
        public HttpServer srv;
        public TcpClient socket;
        public StreamWriter outputStream;
        public string http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();

        private Stream inputStream;
        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient socket, HttpServer srv)
        {
            this.socket = socket;
            this.srv = srv;
        }
        private string StreamReadLine(Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        public void Process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try
            {
                ParseRequest();
                ReadHeaders();
                if (http_method.Equals("GET"))
                {
                    HandleGETRequest();
                }
                else if (http_method.Equals("POST"))
                {
                    HandlePOSTRequest();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.ToString());
                WriteFailure();
            }
            outputStream.Flush();
            inputStream = null; outputStream = null;
            socket.Close();
        }
        public void ParseRequest()
        {
            string request = StreamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];
            Console.WriteLine("starting: " + request);
        }

        public void ReadHeaders()
        {
            Console.WriteLine("readHeaders()");
            string line;
            while ((line = StreamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }
                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                string name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }
                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                httpHeaders[name] = value;
            }
        }
        public void HandleGETRequest()
        {
            srv.HandleGETRequest(this);
        }
        private const int BUF_SIZE = 4096;
        public void HandlePOSTRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 
            Console.WriteLine("get post data start");
            MemoryStream ms = new MemoryStream();
            if (httpHeaders.ContainsKey("Content-Length"))
            {
                int content_len = Convert.ToInt32(httpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(string.Format("POST Content-Length({0}) too big for this simple server", content_len));
                }
                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", to_read);
                    int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Console.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            srv.HandlePOSTRequest(this, new StreamReader(ms));
        }
        public void WriteSuccess()
        {
            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: text/html;charset=utf-8");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }
        public void WriteFailure()
        {
            outputStream.WriteLine("HTTP/1.0 404 File not found");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }
    }

    public abstract class HttpServer
    {
        protected int port;
        protected string ip;
        TcpListener listener;
        bool is_active = true;
        public HttpServer(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }
        public void Listen()
        {
            listener = new TcpListener(IPAddress.Parse(ip), port);
            listener.Start();
            while (is_active)
            {
                TcpClient tc = listener.AcceptTcpClient();
                HttpProcessor processor = new HttpProcessor(tc, this);
                Thread thread = new Thread(new ThreadStart(processor.Process));
                thread.Start();
                Thread.Sleep(1);
            }
        }
        public abstract void HandleGETRequest(HttpProcessor p);
        public abstract void HandlePOSTRequest(HttpProcessor p, StreamReader inputData);
    }

    public class MyHttpServer : HttpServer
    {
        public MyHttpServer(string ip, int port) : base(ip, port)
        {
        }
        public override void HandleGETRequest(HttpProcessor p)
        {
            if (p.http_url == "/help")
            {
                SqlAccess sa = new SqlAccess();
                DataSet ds = sa.SelectWhere("student", new string[] { "id", "name", "age" }, new string[] { "id" }, new string[] { "=" }, new string[] { "1" });
                if (ds != null)
                {
                    JsonData json = new JsonData();
                    DataTable table = ds.Tables[0];
                    foreach (DataRow row in table.Rows)
                    {
                        JsonData data = new JsonData();
                        foreach (DataColumn column in table.Columns)
                        {
                            json[column.ColumnName] = row[column].ToString();
                        }
                    }
                    p.WriteSuccess();
                    p.outputStream.Write(json.ToJson());
                }
                else
                {
                    p.WriteSuccess();
                    p.outputStream.Write("{msg:\"error\"}");
                }
                sa.Close();
            }
            else
            {
                p.WriteSuccess();
                p.outputStream.Write("{msg:\"404\"}");
            }
        }

        public override void HandlePOSTRequest(HttpProcessor p, StreamReader inputData)
        {
            Console.WriteLine("POST request: {0}", p.http_url);
            string data = inputData.ReadToEnd();

            p.outputStream.WriteLine("<html><body><h1>test server</h1>");
            p.outputStream.WriteLine("<a href=/test>return</a><p>");
            p.outputStream.WriteLine("postbody: <pre>{0}</pre>", data);
        }
    }
    public class Program
    {
        public static int Main(String[] args)
        {
            string ip = "192.168.3.216";
            int port = 8080;
            HttpServer httpServer;
            if (args.GetLength(0) > 0)
            {
                httpServer = new MyHttpServer(ip, Convert.ToInt16(args[0]));
            }
            else
            {
                httpServer = new MyHttpServer(ip, port);
            }
            Thread thread = new Thread(new ThreadStart(httpServer.Listen));
            thread.Start();
            return 0;
        }
    }
}
