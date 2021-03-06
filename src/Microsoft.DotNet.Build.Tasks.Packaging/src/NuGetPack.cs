﻿using Microsoft.Build.Framework;
using NuGet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class NuGetPack : PackagingTask
    {

        [Required]
        public ITaskItem[] Nuspecs
        {
            get;
            set;
        }

        [Required]
        public string OutputDirectory
        {
            get;
            set;
        }

        public bool ExcludeEmptyDirectories
        {
            get;
            set;
        }

        public override bool Execute()
        {
            if (Nuspecs == null || Nuspecs.Length == 0)
            {
                Log.LogError("Nuspecs argument must be specified");
                return false;
            }

            if (String.IsNullOrEmpty(OutputDirectory))
            {
                Log.LogError("OuputDirectory argument must be specified");
                return false;
            }

            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            foreach (var nuspec in Nuspecs)
            {
                string nuspecPath = nuspec.GetMetadata("FullPath");

                if (!File.Exists(nuspecPath))
                {
                    Log.LogError($"Nuspec {nuspecPath} does not exist");
                    continue;
                }

                try
                {
                    PackageBuilder builder = new PackageBuilder();

                    using (var nuspecFile = File.Open(nuspecPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        Manifest manifest = Manifest.ReadFrom(nuspecFile);
                        builder.Populate(manifest.Metadata);
                        builder.PopulateFiles(Path.GetDirectoryName(nuspecPath), manifest.Files);
                    }

                    string id = builder.Id, version = builder.Version.ToString();

                    if (String.IsNullOrEmpty(id))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid Id");
                        continue;
                    }

                    if (String.IsNullOrEmpty(version))
                    {
                        Log.LogError($"Nuspec {nuspecPath} does not contain a valid version");
                        continue;
                    }

                    string nupkgPath = Path.Combine(OutputDirectory, $"{id}.{version}.nupkg");

                    using (var fileStream = File.Create(nupkgPath))
                    {
                        builder.Save(fileStream);
                    }

                    Log.LogMessage($"Created '{nupkgPath}'");
                }
                catch (Exception e)
                {
                    Log.LogError($"Error when creating nuget package from {nuspecPath}. {e}");
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
