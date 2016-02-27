// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;

namespace Microsoft.DotNet.Build.Tasks
{
    public class LocatePreviousContract : Task
    {
        [Required]
        public string CurrentContractProjectPath { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Output]
        public string PreviousContractProjectPath { get; set; }

        [Output]
        public string PreviousContractVersion { get; set; }

        public override bool Execute()
        {
            // We trim the last zero as the folder names in the src/ref folders are truncated.
            // eg. AssemblyVersion 4.0.0.0 wil be <AssemblyName>/4.0.0
            AssemblyVersion = AssemblyVersion.Substring(0, AssemblyVersion.LastIndexOf('.'));
            string currentDir = Path.GetDirectoryName(CurrentContractProjectPath);
            string parentDir = currentDir;
            if (!currentDir.EndsWith("ref"))
            {
                parentDir = Path.GetDirectoryName(currentDir);
            }

            Version currentVersion;
            Version maxPreviousVersion = null;

            if (!Version.TryParse(AssemblyVersion, out currentVersion))
            {
                return true;
            }

            foreach (string candidateDir in Directory.EnumerateDirectories(parentDir))
            {
                Version candidateVersion;
                if (Version.TryParse(Path.GetFileName(candidateDir), out candidateVersion)
                    && candidateVersion < currentVersion
                    && (maxPreviousVersion == null || candidateVersion > maxPreviousVersion))
                {
                    maxPreviousVersion = candidateVersion;
                }
            }

            if (maxPreviousVersion == null)
            {
                PreviousContractVersion = String.Empty;
                PreviousContractProjectPath = String.Empty;
            }
            else
            {
                PreviousContractVersion = maxPreviousVersion.ToString();
                string directoryPath = Path.Combine(parentDir, PreviousContractVersion);

                // Appending back the .0 that was initially trimmed to fit src folder name.
                PreviousContractVersion = PreviousContractVersion + ".0";

                PreviousContractProjectPath = Path.Combine(
                    directoryPath,
                    Path.GetFileName(CurrentContractProjectPath));
                if (!File.Exists(PreviousContractProjectPath))
                {
                    PreviousContractProjectPath = Directory.GetFiles(directoryPath,
                        Path.GetFileNameWithoutExtension(CurrentContractProjectPath) + ".*proj")[0];
                }
            }
            return true;
        }
    }
}
