using System.Collections.Generic;
using System.IO;

namespace PowerPosition.Core
{
    /// <summary>
    ///     CSV file dumper
    /// </summary>
    public sealed class CsvWriter
    {
        /// <summary>
        ///     Folder to store csv files
        /// </summary>
        private readonly string _folder;

        /// <summary>
        ///     Header for csv files
        /// </summary>
        private readonly string _header;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="folder">Folder to store csv files</param>
        /// <param name="header">Header for csv files</param>
        public CsvWriter(string folder, string header)
        {
            _folder = folder;
            _header = header;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        /// <summary>
        ///     Write collection of strings to file
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="lines">Strings</param>
        /// <returns>Success</returns>
        public void Dump(string fileName, IEnumerable<string> lines)
        {
            using (var sw = new StreamWriter(Path.Combine(_folder, fileName)))
            {
                sw.WriteLine(_header);
                if (lines != null)
                {
                    foreach (var line in lines)
                        sw.WriteLine(line);
                }
            }
        }
    }
}