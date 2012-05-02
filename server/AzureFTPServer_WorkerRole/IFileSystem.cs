using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace AzureFTPServer_WorkerRole
{
    public interface IFileSystem
    {
        /* format */
        void initialize();

        /* mount file system or intialize the file system. */
        void mount();

        /* list files with specific path */
        IEnumerable<string> dir(string path);

        /*
         * retrieve a stream with specific path
         * if file does not exist, throw FileNotFoundException
         */
        void get(string path, Stream stream);

        /*
         * store file into specified path
         *   if file does not exist, create a new file;
         *   else overwrite the existing file.
         */
        void store(string path, Stream file);
        /* append content to end of file */
        void append(string path, Stream file);

        /* delete a file from FS */
        bool delete(string path);

        /*
         * create directory
         * return false if directory already exists,
         * or true for the creation of a new directory.
         */
        bool mkdir(string path);

        /*
         * remove directory
         * return false if no such directory,
         * or true for the success of directory deletion.
         */
        bool rmdir(string path);

        /*
         * move a file to a new place
         * this method also functions as rename
         */
        void move(string path, string newpath);

        /* duplicate a file with specified name. */
        void copy(string src, string dest);

        /*
         * rename
         * throw FileNotFoundException if src does not exist
         */
        void rename(string src, string dest);

        /* file seperator */
        char getFileSeperator();

        /* root directory */
        string getRootDirectory();
    }
}
