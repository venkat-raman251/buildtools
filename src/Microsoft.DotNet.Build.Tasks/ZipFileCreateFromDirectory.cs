﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    public sealed class ZipFileCreateFromDirectory : Task
    {
        /// <summary>
        /// The path to the directory to be archived.
        /// </summary>
        [Required]
        public string SourceDirectory { get; set; }

        /// <summary>
        /// The path of the archive to be created.
        /// </summary>
        [Required]
        public string DestinationArchive { get; set; }

        /// <summary>
        /// Indicates if the destination archive should be overwritten if it already exists.
        /// </summary>
        public bool OverwriteDestination { get; set; }
        
        /// <summary>
        /// An item group of regular expressions for content to exclude from the archive.
        /// </summary>
        public ITaskItem[] ExcludePatterns { get; set; }

        public override bool Execute()
        {
            if (File.Exists(DestinationArchive) && OverwriteDestination == true)
            {
                Log.LogMessage(MessageImportance.Low, "{0} already existed, deleting before zipping...", SourceDirectory, DestinationArchive);
                File.Delete(DestinationArchive);
            }

            Log.LogMessage(MessageImportance.High, "Compressing {0} into {1}...", SourceDirectory, DestinationArchive);
            if (!Directory.Exists(Path.GetDirectoryName(DestinationArchive)))
                Directory.CreateDirectory(Path.GetDirectoryName(DestinationArchive));

            if (ExcludePatterns == null)
            {
                ZipFile.CreateFromDirectory(SourceDirectory, DestinationArchive);
            }
            else
            {
                // convert to regular expressions
                Regex[] regexes = new Regex[ExcludePatterns.Length];
                for (int i = 0; i < ExcludePatterns.Length; ++i)
                    regexes[i] = new Regex(ExcludePatterns[i].ItemSpec, RegexOptions.IgnoreCase);

                using (FileStream writer = new FileStream(DestinationArchive, FileMode.CreateNew))
                {
                    using (ZipArchive zipFile = new ZipArchive(writer, ZipArchiveMode.Create))
                    {
                        var files = Directory.GetFiles(SourceDirectory, "*", SearchOption.AllDirectories);

                        foreach (var file in files)
                        {
                            // look for a match
                            bool foundMatch = false;
                            foreach (var regex in regexes)
                            {
                                if (regex.IsMatch(file))
                                {
                                    foundMatch = true;
                                    break;
                                }
                            }

                            if (foundMatch)
                            {
                                Log.LogMessage(MessageImportance.Low, "Excluding {0} from archive.", file);
                                continue;
                            }

                            var relativePath = MakeRelativePath(SourceDirectory, file);
                            zipFile.CreateEntryFromFile(file, relativePath, CompressionLevel.Optimal);
                        }
                    }
                }
            }

            return true;
        }

        private string MakeRelativePath(string root, string subdirectory)
        {
            if (!subdirectory.StartsWith(root))
                throw new Exception(string.Format("'{0}' is not a subdirectory of '{1}'.", subdirectory, root));

            // returned string should not start with a directory separator
            int chop = root.Length;
            if (subdirectory[chop] == Path.DirectorySeparatorChar)
                ++chop;

            return subdirectory.Substring(chop);
        }
    }
}
