using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Net;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureFTPServer_WorkerRole
{
    enum NodeType {DIRECTORY, FILE, UNKNOWN};

    class AzureFileSystemNode {
        public AzureFileSystemNode(AzureFileSystemNode parent,
            string path, NodeType type)
        {
            if (parent == null) // root dir
                this.parent = this;
            else
                this.parent = parent;
            this.name = path; //absolute path
            this.type = type;
            /* no children inserted yet */
            if (type == NodeType.DIRECTORY)
            {
                this.children = new Dictionary<string, AzureFileSystemNode>();
                this.children["."] = this;
                this.children[".."] = this.parent;
            }
            else
                this.children = null;
        }

        /* insert a new node into file system give an absolute path */
        public void insert(string relativePath, NodeType type)
        {
            System.Diagnostics.Trace.TraceInformation("absolute path of file: {0}.", relativePath);          
            /* split the path */
            if (type == NodeType.DIRECTORY)
                relativePath = relativePath.Substring(0, relativePath.Length - 1);
            if (relativePath == "") return;

            string[] pathParts = relativePath.Split('/');

            /* acquire lock when updating nodes */
            lock (this)
            {
                AzureFileSystemNode cursor = this;

                for (int i = 0; i < pathParts.Length; i++)
                {
                    string path = pathParts[i];
                    /* initialize when descent node need to be inserted. */
                    if (cursor.children == null)
                    {
                        cursor.children = new Dictionary<string, AzureFileSystemNode>();
                    }

                    AzureFileSystemNode preceed = null;

                    try
                    {
                        preceed = cursor.children[path];
                    }
                    catch (KeyNotFoundException)
                    {
                        if (i == pathParts.Length - 1)
                        {
                            if( type == NodeType.DIRECTORY )
                                preceed = cursor.children[path] =
                                    new AzureFileSystemNode(cursor, cursor.name+path+"/",
                                        type);
                            else
                                preceed = cursor.children[path] =
                                   new AzureFileSystemNode(cursor, cursor.name + path,
                                       type);
                        }
                        else {
                            preceed = cursor.children[path] =
                                new AzureFileSystemNode(cursor, path,
                                    NodeType.DIRECTORY);
                        }
                        System.Diagnostics.Trace.TraceInformation(
                            "Create Azure File System node {0}.", path);
                    }
                    cursor = preceed;
                }

                /* cursor now last file system node */
                System.Diagnostics.Trace.TraceInformation("aaaaaaaaaaa {0}", type);
            }
        }
        
        /* find file system node relative to current path */
        public AzureFileSystemNode find(string relativePath)
        {
            if (relativePath == "")
                return this;
            if (relativePath.EndsWith("/"))
                relativePath = relativePath.Substring(0, relativePath.Length - 1);
            string[] pathParts = relativePath.Split('/');
            AzureFileSystemNode cursor = this;

            lock (this)
            {
                try
                {
                    foreach (string path in pathParts)
                    {
                        cursor = cursor.children[path];
                    }
                }
                catch (KeyNotFoundException e)
                {
                    System.Diagnostics.Trace.TraceError(
                        "Node not found {0}. {1}", relativePath, e.Message);
                    cursor = null;
                }
                catch (NullReferenceException e)
                {
                    /* must be children field of some node not initialized */
                    System.Diagnostics.Trace.TraceError(
                        "Node not found {0}. {1}", relativePath, e.Message);
                    cursor = null;
                }
            }

            return cursor;
        }

        /* delete a node relative to current path */
        public void delete(string relativePath)
        {
            AzureFileSystemNode node = find(relativePath);
            if (node != null)
            {
                lock (this)
                {
                    if (node.canBeDeleted(node.type))
                    {
                        foreach (string key in node.parent.children.Keys) {
                            if (node == node.parent.children[key]) {
                                node.parent.children.Remove(key);
                                break;
                            }
                                 
                        }
                        System.Diagnostics.Trace.TraceInformation("remove {0}'s {1}", node.parent.name, relativePath);
                    }
                }
            }
        }

        public bool canBeDeleted(NodeType type)
        {
            if (type == NodeType.DIRECTORY)
                return (children.Count == 2);
            else
                return true;
        }

        private string[] getPathParts(string path)
        {
            if (path == "/")
                return null;

            bool isDirectory = (path[path.Length - 1] ==
                AzureFileSystem.AFS_FILE_SEPERATOR);
            /* split the path */
            string[] pathParts = null;
            if (isDirectory)
            {
                pathParts = path.Substring(1, path.Length - 2).Split('/');
            }
            else
            {
                pathParts = path.Substring(1).Split('/');
            }

            return pathParts;
        }

        public string name { get; set; }
        public NodeType type { get; set; }
        public AzureFileSystemNode parent { get; set; }
        public Dictionary<string, AzureFileSystemNode> children { get; set; }
    }

    public class AzureFileSystem : IFileSystem
    {
        public AzureFileSystem()
        {
            _initialized = false;
            _rootPath = null;
            _rootNode = null;

        }

        /* format device */
        public void initialize(string id)
        {
            /* file system already initialized, return ASAP */
            if (_initialized == true)
            {
                return;
            }

            /*
             * no need to synchronize,
             * since when intialize the file system, only one process/thread
             * is active
             */
            try
            {
                Microsoft.WindowsAzure.CloudStorageAccount.
                    SetConfigurationSettingPublisher(
                        (configName, configSetter) =>
                        {
                            configSetter(RoleEnvironment.
                                GetConfigurationSettingValue(configName));
                        }
                    );
                var storageAccount =
                    CloudStorageAccount.FromConfigurationSetting(
                    "DataConnectionString");
                var client = storageAccount.CreateCloudBlobClient();
                /* get root path, create if not exists */
                var user_container = client.GetContainerReference("user");
                var blob = user_container.GetBlobReference("user/container/"+id);
                _rootPath = client.GetContainerReference(blob.DownloadText());
                _rootPath.CreateIfNotExist();
                Stream root = new MemoryStream();
                var root_blob = _rootPath.GetBlockBlobReference("/");
                root_blob.Properties.ContentType = AFS_BINARY_MODE;
                root_blob.UploadFromStream(root);
                root.Close();
                
    //            CloudBlockBlob blob = _rootPath.GetBlockBlobReference("a/");
    //           blob.Properties.ContentType = "application/octet-stream";
    //            Stream a = new MemoryStream();
    //            blob.UploadFromStream(a);
    //            a.Close();
                
                /* create root node */
                _rootNode = new AzureFileSystemNode(null, AFS_ROOT,
                    NodeType.DIRECTORY);
                
               
            }
            catch (WebException e)
            {
                System.Diagnostics.Trace.TraceError(
                    "An error occurs while intializing Azure file system. {0}",
                    e.Message);
            }

            _initialized = true;
            System.Diagnostics.Trace.TraceInformation(
                "Azure file system format complete!");

        }

        /* intitialize file system */
        public void mount(string mountrootpath)
        {
            if (blobContainerExists(_rootPath))
            {
                string basePath = _rootPath.Uri.AbsolutePath;
                IEnumerable<IListBlobItem> enumerator = _rootPath.ListBlobs(
                    new BlobRequestOptions { UseFlatBlobListing = true });
                foreach (IListBlobItem item in enumerator)
                {
                    string relativePath = item.Uri.AbsolutePath.Substring(
                        basePath.Length+1);
                    if (relativePath.StartsWith(mountrootpath)){
                        relativePath = relativePath.Substring(AFS_ROOT.Length);
                        if(relativePath.Length == 0)
                            continue;
                        if (relativePath.EndsWith("/"))
                            _rootNode.insert(relativePath, NodeType.DIRECTORY);
                        else
                            _rootNode.insert(relativePath, NodeType.FILE);
                    }
                }
            }
        }

        /*
         * TODO
         */
        public IEnumerable<string> dir(string path)
        {
            System.Diagnostics.Trace.TraceInformation("dir path {0}.", path);
            AzureFileSystemNode node = _rootNode.find(path.Substring(AFS_ROOT.Length));
            LinkedList<string> files = new LinkedList<string>();
            if (node != null && node.children != null)
            {
                string ownerPerm = "rw-";
                string groupPerm = "---";
                string otherPerm = "---";
                char delimiter = ' ';
                string owner = "undefined"; 
                string group = _rootPath.Attributes.Name;
                foreach (String childpath in node.children.Keys){
                    string filename = node.name + childpath;
                    System.Diagnostics.Trace.TraceInformation("file name: {0}", filename);
                    long blobSize = 0;
                    char directory = 'd';
                    if (node.children[childpath].type == NodeType.FILE)
                    {
                        directory = '-';
                        CloudBlockBlob blob =
                           _rootPath.GetBlockBlobReference(filename);
                        blob.FetchAttributes();
                        blobSize = blob.Properties.Length;
                    }
                    System.Diagnostics.Trace.TraceInformation("blob size: {0}.", blobSize);
                    StringBuilder sb = new StringBuilder();
                    sb.Append(directory);
                    sb.Append(ownerPerm);
                    sb.Append(groupPerm);
                    sb.Append(otherPerm);
                    sb.Append(delimiter);
                    sb.Append(owner);
                    sb.Append(delimiter);
                    sb.Append(group);
                    sb.Append(String.Format("{0,13}", blobSize));
                    sb.Append(delimiter);
                    sb.Append(childpath);

                    files.AddLast(sb.ToString());
                    System.Diagnostics.Trace.TraceInformation("file information {0}.", sb.ToString());
                }

                return files;
            }
            else if (node != null)
            {
                return null;
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public string find(string path) {
            AzureFileSystemNode node = _rootNode.find(path.Substring(AFS_ROOT.Length));
            if (node == null)
                return null;
            else
                return node.name;
        }
        public void get(string path, Stream stream)
        {
            AzureFileSystemNode node = _rootNode.find(path.Substring(AFS_ROOT.Length));
            if (node != null && node.type == NodeType.FILE )
            {
                if (stream != null)
                {
                    System.Diagnostics.Trace.TraceInformation("node name: {0}.", path);
                    CloudBlockBlob blob =
                        _rootPath.GetBlockBlobReference(node.name);
                    blob.DownloadToStream(stream);
                }
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public void store(string path, Stream file)
        {
            System.Diagnostics.Trace.TraceInformation("file {0}, base {1}.", path, AFS_ROOT);
            /* blob id is relative to ROOT path */
            string relativePath = path.Substring(AFS_ROOT.Length);
            _rootNode.insert(relativePath, NodeType.FILE);
            AzureFileSystemNode node = _rootNode.find(relativePath);
            CloudBlockBlob blob =
                _rootPath.GetBlockBlobReference(node.name);
            blob.UploadFromStream(file);
            return;
        }

        public void append(string path, Stream file)
        {
            return;
        }

        public bool delete(string path)
        {
            return delete(path, NodeType.FILE);
        }

        /*
         * we use an empty file tailing with '/' to mimic directory on azure.
         * path must be an absolute one
         */
        public bool mkdir(string path)
        {
            Stream dir = new MemoryStream();
            /* transform absolute path to relative one */
            string relative = path.Substring(AFS_ROOT.Length);
            
            AzureFileSystemNode node = _rootNode.find(relative);
            if (node == null)
            {
                /* prepare migration to multi-threaded environment */
                lock (_rootPath)
                {
                    _rootNode.insert(relative, NodeType.DIRECTORY);
                    AzureFileSystemNode newnode = _rootNode.find(relative);
                    CloudBlockBlob blob = _rootPath.GetBlockBlobReference(newnode.name);
                    blob.Properties.ContentType = AFS_BINARY_MODE;
                    blob.UploadFromStream(dir);
                    
                }
            }

            return (node == null);
        }

        public bool rmdir(string path)
        {
            return delete(path, NodeType.DIRECTORY);
        }

        public void move(string path, string newpath)
        {
            return;
        }

        public void copy(string src, string dest)
        {
            return;
        }

        public char getFileSeperator()
        {
            return AFS_FILE_SEPERATOR;
        }

        public string getRootDirectory()
        {
            return AFS_ROOT;
        }

        public void rename(string src, string dest)
        {
        }

        private bool delete(string path, NodeType type)
        {
            string relative = path.Substring(AFS_ROOT.Length);
            AzureFileSystemNode node = _rootNode.find(relative);
            if (node != null && !node.canBeDeleted(type))
            {
                throw new DeleteFailException();
            }

            if (node != null)
            {
                /* prepare migration to multi-threaded environment */
                CloudBlockBlob blob = _rootPath.GetBlockBlobReference(node.name);
                blob.DeleteIfExists();
                _rootNode.delete(relative);
            }

            return (node != null);
        }

        /*
         * this snippet is from
         * http://blog.smarx.com/posts/testing-existence-of-a-windows-azure-blob
         * to test existance of a specific blob container.
         * When *FetchAttributes* called, a HEAD request is sent against
         * the blob container.
         */
        private bool blobContainerExists(CloudBlobContainer container)
        {
            try
            {
                container.FetchAttributes();

                return true;
            }
            catch (StorageClientException e)
            {
                if (e.ErrorCode == StorageErrorCode.BlobNotFound)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        private CloudBlobContainer _rootPath;
        private AzureFileSystemNode _rootNode;
        private bool _initialized;

        /* constants */
        public const char AFS_FILE_SEPERATOR = '/';
                                            /* mount point on cloud end */
        private const string AFS_ROOT = "/";          /* root directory */
        /* mime type for uploaded file */
        private const string AFS_BINARY_MODE = "application/octet-stream";
        private const string AFS_ASCII_MODE = "plain/text";
    }
}
