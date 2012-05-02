using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AzureFTPServer_WorkerRole
{
    class FTPServerSlave
    {
        /* static block, invoked when intializing the class */
        static FTPServerSlave(){
            /* build verb dictionary */
            buildVerbDictionary();
        }

        public FTPServerSlave(FTPServer server,
            Dictionary<string, string> accounts,
            IFileSystem filesystem, TcpClient connection)
        {
            this._server = server;
            this._accounts = accounts;
            this._filesystem = filesystem;
            this._connection = connection;
            
            /* default active end point */
            this._activeEndPoint = new IPEndPoint(
                ((IPEndPoint)_connection.Client.RemoteEndPoint).Address, 20);

            NetworkStream stream = this._connection.GetStream();
            this._controlInput = new StreamReader(stream);
            this._controlOutput = new StreamWriter(stream);
            _controlOutput.AutoFlush = true;
        }

        public void run()
        {
            /* greeting the client */
            _controlOutput.Write(SC_GREET + " " + GREETING + CRLF);
            System.Diagnostics.Trace.TraceInformation("Greeting done!");

            try
            {
                while (!this._quit)
                {
                    string request = this._controlInput.ReadLine();
                    string resp = null;
                    System.Diagnostics.Trace.TraceInformation(
                        "Recieve request {0}.", request);
                    if (request != null)
                    {
                        /* client still connecting */
                        resp = response(request);
                        if (resp != null)
                        {
                            _controlOutput.Write(resp);
                            _controlOutput.Flush();
                        }
                    }
                    else
                    {
                        /* client disconnect */
                        System.Diagnostics.Trace.TraceInformation("Client disconnected.");

                        break;
                    }
                }
            }
            finally
            {
                /*
                 * client request to quit.
                 * normally, this should not be reached.
                 */
                slaveQuit();
            }
        }

        /*
         * handle ftp request and response.
         * selected ftp protocol is impelemented here.
         * 
         * method return a mark. If extra file operation required,
         * they are handled internally.
         */
        public string response(string request)
        {
            System.Diagnostics.Trace.TraceInformation(
                "FTP server receive request {0}.", request);
            StringBuilder sb = new StringBuilder();
            /* find first white space and extract verb */
            int index = request.IndexOf(DELIMITER);
            string verb = null;
            string parameter = null;
            if (index != -1)
            {
                verb = request.Substring(0, index);
                parameter = request.Substring(index + 1).Trim();
            }
            else
            {
                verb = request;
                parameter = null;
            }

            try
            {
                Verb v = _verbDict[verb];
                /* operate before login or duplicated login */
                if (_authenticated && (v == Verb.USER || v == Verb.PASS))
                {
                    throw new DuplicatedLoginException();
                }
                else if (!(_authenticated ||
                   (((_username == null) && (v == Verb.USER)) ||
                    ((_username != null) && (v == Verb.PASS)))))
                {
                    _username = null;
                    throw new IncorrectLoginException();
                }

                /* process request case by case */
                switch (v)
                {
                    case Verb.USER:
                        System.Diagnostics.Trace.TraceInformation("Account {0} " +
                            "request to login.", parameter);
                        _username = parameter;
                        sb.Append(SC_REQ_PW);
                        sb.Append(DELIMITER);
                        sb.Append(SM_REQ_PW);

                        break;
                    case Verb.PASS:
                        string password = parameter;
                        _authenticated = authenticate(_username, password);
                        if (_authenticated)
                        {
                            /* remember current user path */
                            _currentPath = _filesystem.getRootDirectory() +
                                _username + _filesystem.getFileSeperator();
                            /* compose response information */
                            sb.Append(SC_LN_SUC);
                            sb.Append(DELIMITER);
                            sb.Append(SM_LN_SUC);
                        }
                        else
                        {
                            sb.Append(SC_LN_FAIL);
                            sb.Append(DELIMITER);
                            sb.Append(SM_LN_FAIL);

                            _username = null;
                            _currentPath = null;
                        }

                        break;
                    case Verb.CWD:
                        {
                            string directoryChanged = null;
                            if (parameter == "..")
                            {
                                directoryChanged = gotoParent(_currentPath);
                            }
                            else if (parameter == null || parameter == "" ||
                                parameter == ".")
                            {
                                directoryChanged = _currentPath;
                            }
                            else
                            {
                                parameter = getAbsolutePath(parameter);
                                directoryChanged =
                                    changeDirectory(parameter);
                                System.Diagnostics.Trace.TraceInformation("directory changed to {0}.", _currentPath);
                            }

                            if (directoryChanged != null)
                            {
                                _currentPath = directoryChanged;

                                sb.Append(SC_DIR_CHG_OK);
                                sb.Append(DELIMITER);
                                sb.Append(SM_DIR_CHG_OK);
                            }
                            else
                            {
                                sb.Append(SC_DIR_CHG_FAIL);
                                sb.Append(DELIMITER);
                                sb.Append(SM_DIR_CHG_FAIL);
                            }
                        }
                        break;
                    case Verb.PWD:
                        sb.Append(SC_PWD);
                        sb.Append(DELIMITER);
                        sb.AppendFormat(SM_PWD_FORMAT, _currentPath);

                        break;
                    case Verb.CDUP:
                        _currentPath = gotoParent(_currentPath);

                        sb.Append(SC_DIR_CHG_OK);
                        sb.Append(DELIMITER);
                        sb.Append(SM_DIR_CHG_OK);

                        break;
                    case Verb.PASV:
                        {
                            _connectionMode = ConnectionMode.POSITIVE;

                            if (_passiveEndPoint != null)
                            {
                                System.Diagnostics.Trace.TraceInformation("prepare to release lock on endpoint.");
                                _server.releasePassivePort(_passiveEndPoint);
                                _passiveEndPoint = null;
                                System.Diagnostics.Trace.TraceInformation("release lock on endpoint.");
                            }
                            _passiveEndPoint = _server.acquirePassivePort();
                            string passiveResponse =
                                composePassiveResponse(_connection, _passiveEndPoint);
                            sb.Append(SC_PASV);
                            sb.Append(DELIMITER);
                            sb.Append(passiveResponse);
                            sb.Append(CRLF);
                        }

                        break;
                    case Verb.PORT:
                        {
                            _connectionMode = ConnectionMode.ACTIVE;

                            string[] paramParts = parameter.Split(',');
                            string ipAddress = String.Join(".", paramParts, 0, 4);
                            _activeEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress),
                                Convert.ToInt16(paramParts[4]) * 256 +
                                Convert.ToInt16(paramParts[5]));

                            sb.Append(SC_PORT);
                            sb.Append(DELIMITER);
                            sb.Append(SM_PORT_OK);
                        }

                        break;
                    case Verb.TYPE:
                        switch (parameter[0])
                        {
                            case 'I':
                                _transferMode = TransferMode.BINARY;
                                break;
                            case 'A':
                                _transferMode = TransferMode.ASCII;
                                break;
                            default:
                                _transferMode = TransferMode.ASCII;
                                break;
                        }

                        sb.Append(SC_MODE);
                        sb.Append(DELIMITER);
                        sb.AppendFormat(SM_MODE_FORMAT, parameter[0]);

                        break;
                    case Verb.QUIT:
                        sb.Append(SC_QUIT);
                        sb.Append(DELIMITER);
                        sb.Append(SM_QUIT);
                        /* set quit flag */
                        _quit = true;

                        break;
                    case Verb.LIST:
                        {
                            /*
                             * if list directory not set,
                             * list files in current path by default
                             */
                            if (parameter == null)
                            {
                                parameter = _currentPath;
                            }
                            string dirPath = getAbsolutePath(parameter);
                            IEnumerable<string> files = null;
                            try
                            {
                                files = _filesystem.dir(dirPath);
                                /* compose response */
                                StringBuilder sbData = new StringBuilder();
                                sb.Append(SC_LIST);
                                sb.Append(DELIMITER);
                                sb.Append(SM_LIST);
                                sb.Append(CRLF);
                                _controlOutput.Write(sb.ToString());
                                sb.Clear();

                                TcpClient socket = getDataConnection();
                                NetworkStream stream = socket.GetStream();
                                StreamWriter output = new StreamWriter(stream);

                                if (files != null)
                                {
                                    foreach (var file in files)
                                    {
                                        sbData.Append(file);
                                        sbData.Append(CRLF);
                                    }
                                }
                                System.Diagnostics.Trace.TraceInformation("file information: {0}.", sbData.ToString());
                                // sbData.Append("150 OK");
                                // sbData.Append("-rw-r--r-- 1 qiush lab           33 Mar 31 15:32 1020.log" + CRLF);

                                /* transfer data and close the connection */
                                output.Write(sbData.ToString());
                                output.Flush();
                                output.Close();
                                socket.Close();

                                /* listing complete */
                                sb.Append(SC_TRANS_COMP);
                                sb.Append(DELIMITER);
                                sb.Append(SM_TRANS_COMP);
                            }
                            catch (FileNotFoundException)
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_NOF_FORMAT, verb,
                                    parameter);
                            }
                        }
                        break;
                    case Verb.RETR:
                        {
                            string absPath = getAbsolutePath(parameter);
                            _transferMode = TransferMode.BINARY;
                            sb.Append(SC_FS_PRE);
                            sb.Append(DELIMITER);
                            sb.AppendFormat(SM_FS_TRANS_FORMAT, parameter);
                            sb.Append(CRLF);
                            _controlOutput.Write(sb.ToString());
                            sb.Clear();

                            TcpClient socket = getDataConnection();
                            NetworkStream stream = socket.GetStream();
                            try
                            {
                                _filesystem.get(absPath, stream);
                                sb.Append(SC_TRANS_COMP);
                                sb.Append(DELIMITER);
                                sb.Append(SM_TRANS_COMP);

                                stream.Close();
                                socket.Close();
                            }
                            catch (FileNotFoundException)
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.Append(SM_TRANS_COMP);
                            }
                        }

                        break;
                    case Verb.STOR:
                        {
                            /* STOR will overwrite existing file */
                            string absPath = getAbsolutePath(parameter);
                            System.Diagnostics.Trace.TraceInformation("a {0}, b {1}.", parameter, absPath);

                            _transferMode = TransferMode.BINARY;
                            sb.Append(SC_FS_PRE);
                            sb.Append(DELIMITER);
                            sb.AppendFormat(SM_FS_TRANS_FORMAT, parameter);
                            sb.Append(CRLF);
                            _controlOutput.Write(sb.ToString());
                            sb.Clear();

                            TcpClient socket = null;
                            socket = getDataConnection();
                            NetworkStream stream = socket.GetStream();
                            _filesystem.store(absPath, stream);
                            System.Diagnostics.Trace.TraceInformation("transfer done.");
                            byte[] buffer = new byte[2];
                            stream.Read(buffer, 0, 2);
                            stream.Close();
                            socket.Close();

                            sb.Append(SC_FS_OK);
                            sb.Append(DELIMITER);
                            sb.Append(SM_TRANS_COMP);
                        }

                        break;
                    case Verb.APPE:
                        {
                            /* STOR will overwrite existing file */
                            string absPath = getAbsolutePath(parameter);

                            _transferMode = TransferMode.BINARY;
                            sb.Append(SC_FS_PRE);
                            sb.Append(DELIMITER);
                            sb.AppendFormat(SM_FS_TRANS_FORMAT, parameter);
                            _controlOutput.Write(sb.ToString());
                            sb.Clear();

                            TcpClient socket = null;
                            socket = getDataConnection();
                            NetworkStream stream = socket.GetStream();
                            _filesystem.append(absPath, stream);
                            stream.Close();
                            socket.Close();

                            sb.Append(SC_FS_OK);
                            sb.Append(DELIMITER);
                        }

                        break;
                    case Verb.MKD:
                        {
                            string absPath = getAbsolutePath(parameter);
                            bool dirCreated = _filesystem.mkdir(absPath);

                            if (dirCreated)
                            {
                                sb.Append(SC_DIR_CREATED);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_DIR_CREATE_FORMAT, absPath);
                            }
                            else
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_EXISTS_FORMAT,
                                    verb, parameter);
                            }
                        }
                        break;
                    case Verb.RMD:
                        {
                            string absPath = getAbsolutePath(parameter);
                            if (absPath[parameter.Length - 1] !=
                                _filesystem.getFileSeperator())
                            {
                                absPath = absPath + _filesystem.getFileSeperator();
                            }
                            bool dirRemoved = _filesystem.rmdir(absPath);

                            if (dirRemoved)
                            {
                                sb.Append(SC_FS_OK);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_OK_FORMAT, verb);
                            }
                            else
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_NOF_FORMAT, verb,
                                    parameter);
                            }
                        }

                        break;
                    case Verb.DELE:
                        {
                            string absPath = getAbsolutePath(parameter);
                            bool fileRemoved = _filesystem.delete(absPath);

                            if (fileRemoved)
                            {
                                sb.Append(SC_FS_OK);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_OK_FORMAT, verb,
                                    parameter);
                            }
                            else
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_NOF_FORMAT, verb,
                                    parameter);
                            }
                        }

                        break;
                    case Verb.RNFR:
                        {
                            srcName = getAbsolutePath(parameter);
                        }

                        break;
                    case Verb.RNTO:
                        {
                            string destName = getAbsolutePath(parameter);
                            try
                            {
                                _filesystem.rename(srcName, destName);
                                sb.Append(SC_FS_REREADY);
                                sb.Append(DELIMITER);
                                sb.Append(SM_FS_REREADY);
                                sb.Append(CRLF);
                                _controlOutput.Write(sb.ToString());
                                sb.Clear();
                                sb.Append(SC_FS_OK);
                                sb.Append(DELIMITER);
                                sb.Append(SM_FS_REOK);
                            }
                            catch (FileNotFoundException)
                            {
                                sb.Append(SC_FS_FAIL);
                                sb.Append(DELIMITER);
                                sb.AppendFormat(SM_FS_NOF_FORMAT2, srcName);
                            }
                        }

                        break;
                    default:
                        sb.Append(SC_NIMP);
                        sb.Append(DELIMITER);
                        sb.Append(SM_NIMP);

                        break;
                }
            }
            catch (KeyNotFoundException e)
            {
                System.Diagnostics.Trace.TraceError(
                    "Verb {0} not implemented yet. {1}",
                    verb, e.Message);
                /* construct response */
                sb.Append(SC_NIMP);
                sb.Append(DELIMITER);
                sb.Append(verb);
                sb.Append(DELIMITER);
                sb.Append(SM_NIMP);
            }
            catch (IncorrectLoginException e)
            {
                System.Diagnostics.Trace.TraceError(
                    "Incorrect login. {0}", e.Message);
                /* construct response */
                sb.Append(SC_LN_FAIL);
                sb.Append(DELIMITER);
                sb.Append(SM_LN_INCORRECT);
            }
            catch (DuplicatedLoginException e)
            {
                System.Diagnostics.Trace.TraceError(
                    "Duplicated login. {0}", e.Message);
                /* construct response */
                sb.Append(SC_LN_DUP);
                sb.Append(DELIMITER);
                sb.Append(SM_LN_DUP);
            }
            catch (DeleteFailException e)
            {
                System.Diagnostics.Trace.TraceError(
                    "Fail to delete file or directory. {0}", e.Message);
                /* construct response */
                sb.Append(SC_FS_FAIL);
                sb.Append(DELIMITER);
                sb.AppendFormat(SM_FS_NOT_PERMIT_FORMAT, parameter);
            }

            sb.Append(CRLF);            /* end of response */
            System.Diagnostics.Trace.TraceInformation("Response from server " +
                "{0}", sb.ToString());

            return sb.ToString();
        }

        /* get absolute path */
        private string getAbsolutePath(string path)
        {
            string absPath = null;

            if (path.IndexOf(_filesystem.getFileSeperator()) == 0)
            {
                absPath = path;
            }
            else
            {
                absPath = _currentPath + path;
            }

            return absPath;
        }

        /* TODO */
        private bool authenticate(string username, string password)
        {
            return true;
        }

        private string composePassiveResponse(TcpClient client, IPEndPoint port)
        {
            /* the extra character is required for backward compatibility. */
            StringBuilder sb = new StringBuilder();
            IPEndPoint myside = _connection.Client.LocalEndPoint as IPEndPoint;
            sb.Append(myside.Address.ToString().Replace('.', ','));
            sb.Append(',');
            sb.Append(port.Port / 256);
            sb.Append(',');
            sb.Append(port.Port % 256);

            return sb.ToString();
        }

        /* if a path is valid, then its parent must be exist. */
        private string gotoParent(string dir)
        {
            if (dir == _filesystem.getRootDirectory())
            {
                /* parent of root directory is still root. */
                return null;
            }
            else
            {
                /* for dir path, a tailing seperator should be skipped */
                int index = dir.LastIndexOf(_filesystem.getFileSeperator(),
                    dir.Length - 2);
                /* result dir should also include a tailing seperatror */
                dir = dir.Substring(0, index + 1);
            }

            return dir;
        }

        private TcpClient getDataConnection()
        {
            TcpClient socket = null;
            if (_connectionMode == ConnectionMode.POSITIVE)
            {
                /* initialize the connection late and release fast */
                TcpListener listener = new TcpListener(_passiveEndPoint);
                listener.Start();
                socket = listener.AcceptTcpClient();
                listener.Stop();
            }
            else if (_connectionMode == ConnectionMode.ACTIVE)
            {
                socket = new TcpClient();
                socket.Connect(_activeEndPoint);
            }

            return socket;
        }

        private string changeDirectory(string path)
        {
            if (path[path.Length - 1] != _filesystem.getFileSeperator())
                path = path + _filesystem.getFileSeperator();

            try
            {
                _filesystem.get(path, null);
                return path;
            }
            catch (FileNotFoundException)
            {
            }

            return null;
        }

        private static void buildVerbDictionary()
        {
            _verbDict = new Dictionary<string, Verb>();
            /* control verb */
            _verbDict[USER] = Verb.USER;
            _verbDict[PASS] = Verb.PASS;
            _verbDict[CWD] = Verb.CWD;
            _verbDict[PWD] = Verb.PWD;
            _verbDict[CDUP] = Verb.CDUP;
            _verbDict[PASV] = Verb.PASV;
            _verbDict[PORT] = Verb.PORT;
            _verbDict[TYPE] = Verb.TYPE;
            _verbDict[QUIT] = Verb.QUIT;
            /* file access verb */
            _verbDict[LIST] = Verb.LIST;
            _verbDict[RETR] = Verb.RETR;
            _verbDict[STOR] = Verb.STOR;
            _verbDict[APPE] = Verb.APPE;
            _verbDict[MKD] = Verb.MKD;
            _verbDict[RMD] = Verb.RMD;
            _verbDict[DELE] = Verb.DELE;
            _verbDict[RNFR] = Verb.RNFR;
            _verbDict[RNTO] = Verb.RNTO;
        }

        private void slaveQuit()
        {
            if (_passiveEndPoint != null)
            {
                _server.releasePassivePort(_passiveEndPoint);
                _passiveEndPoint = null;
            }
            _controlInput.Close();
            _controlOutput.Close();
            _connection.Close();
        }

        /* utility objects */
        private static Dictionary<string, Verb> _verbDict;

        /* state objects(NOTE: FTP is a state-ful protocol) */
        private FTPServer _server;
        private Dictionary<string, string> _accounts;
        private IFileSystem _filesystem;
        private TcpClient _connection;

        private string _username;
        private bool _authenticated = false;
        private string _currentPath;
        private bool _quit = false;

        private IPEndPoint _passiveEndPoint;
        private IPEndPoint _activeEndPoint;
        private StreamWriter _controlOutput;
        private StreamReader _controlInput;
        private TransferMode _transferMode = TransferMode.ASCII; /* ascii mode by default */
        private ConnectionMode _connectionMode = ConnectionMode.ACTIVE;

        /* for verb RNFR */
        private string srcName;


        /* constants */
        private const string CRLF = "\r\n";
        private const string DELIMITER = " ";

        enum Verb
        {
            USER, PASS, CWD, PWD, CDUP, PASV, PORT, TYPE, QUIT,
            LIST, RETR, STOR, APPE, MKD, RMD, DELE, RNFR, RNTO
        };
        /* request verbs */
        /* control verbs */
        private const string USER = "USER";     /* username */
        private const string PASS = "PASS";     /* password */
        private const string CWD = "CWD";       /* change directory */
        private const string PWD = "PWD";       /* print directory */
        private const string CDUP = "CDUP";     /* go up level */
        private const string PASV = "PASV";     /* passive mode */
        private const string PORT = "PORT";     /* negotiate port */
        private const string TYPE = "TYPE";     /* a or b? */
        private const string QUIT = "QUIT";     /* say goodbye */
        /* file access verbs */
        private const string LIST = "LIST";
        private const string RETR = "RETR";     /* retrieve file */
        private const string STOR = "STOR";     /* upload file */
        private const string APPE = "APPE";     /* append file */
        private const string MKD = "MKD";
        private const string RMD = "RMD";
        private const string DELE = "DELE";
        private const string RNFR = "RNFR";
        private const string RNTO = "RNTO";

        /* status code */
        private const string SC_LIST = "150";    /* begin list transform */
        private const string SC_FS_PRE = "150";  /* prepare operation on fs */
        private const string SC_MODE = "200";    /* transfer mode */
        private const string SC_PORT = "200";    /* possitive mode open */
        private const string SC_PASV = "200";    /* passive mode open */
        private const string SC_GREET = "220";   /* greeting */
        private const string SC_QUIT = "221";    /* quit */
        private const string SC_TRANS_COMP = "226";/* data sending complete */
        private const string SC_LN_SUC = "230";  /* login successfully */
        private const string SC_DIR_CHG_OK = "250";
        /* directory changed successfully */
        private const string SC_FS_OK = "250";   /* fs related oper succeed */
        private const string SC_PWD = "257";     /* pwd accepted */
        private const string SC_DIR_CREATED = "257";
        /* directory created */
        private const string SC_REQ_PW = "331";  /* require password */
        private const string SC_FS_REREADY = "350";/* ready to rename */
        private const string SC_NIMP = "502";    /* verb not imp-ed */
        private const string SC_LN_DUP = "503";  /* duplicated login */
        private const string SC_LN_FAIL = "530"; /* login fail */
        private const string SC_DIR_CHG_FAIL = "550";
        /* directory change fails */
        private const string SC_FS_FAIL = "550"; /* file system related fail */

        /* greetings when client connection established */
        private const string GREETING = "(Azure Cloud FTP 1.0)";
        /* status mssage */
        private const string SM_QUIT = "Bye.";
        private const string SM_LIST = "Here comes the directory listing.";
        private const string SM_FS_TRANS_FORMAT =
            "Opening BINARY mode data connection for {0}.";
        private const string SM_MODE_FORMAT = "Type set to {0}.";
        private const string SM_PORT_OK = "Change to active mode.";
        private const string SM_TRANS_COMP =
            "Transfer complete.";   /* transfer ok for both file & directory */
        private const string SM_LN_SUC =
            "Login successful.";                        /* login successful */
        private const string SM_DIR_CHG_OK =
            "Directory successfully changed.";          /* dir change succ */
        private const string SM_FS_OK_FORMAT =
            "{0} command succeeded.";
        private const string SM_FS_REOK =
            "Rename successful.";
        private const string SM_PWD_FORMAT =
            "\"{0}\" is the current directory.";        /* pwd */
        private const string SM_DIR_CREATE_FORMAT =
            "\"{0}\" - Directory successfully created.";
        private const string SM_REQ_PW =
            "Please specify the password.";             /* require passwd */
        private const string SM_FS_REREADY =
            "File or directory exists, ready for destination name.";
        private const string SM_NIMP =
            " not implemented.";                        /* verb not imp-ed */
        private const string SM_LN_DUP =
            "You are already logged in.";               /* duplicated login */
        private const string SM_LN_FAIL =
            "Login incorrect.";                         /* login fail */
        private const string SM_LN_INCORRECT =
            "Please login first before operation.";     /* incorrect login */
        private const string SM_DIR_CHG_FAIL =
            "Fail to change directory.";                /* dir change fails */
        private const string SM_FS_NOT_PERMIT_FORMAT =
            "{0}: Operation not permitted.";    /* delete non-empty dir */
        private const string SM_FS_EXISTS_FORMAT =
            "{0} command fails. {1}: File or directory exists.";
        private const string SM_FS_NOF_FORMAT =
            "{0} command fails. {1}: No such file or directory.";
        private const string SM_FS_NOF_FORMAT2 =
            "{0}: No such file or directory.";
    }
}
