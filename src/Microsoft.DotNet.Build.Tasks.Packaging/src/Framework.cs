// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class FrameworkSet
    {
        private const string LastNonSemanticVersionsFileName = "LastNonSemanticVersions.xml";

        // avoid parsing the same documents multiple times on a single node.
        private static Dictionary<string, FrameworkSet> s_frameworkSetCache = new Dictionary<string, FrameworkSet>();
        private static object s_frameworkSetCacheLock = new object();

        public FrameworkSet()
        {
            Frameworks = new Dictionary<string, SortedSet<Framework>>();
            LastNonSemanticVersions = new Dictionary<string, Version>();
        }

        public static FrameworkSet Load(string frameworkListsPath)
        {
            FrameworkSet result;
            if (s_frameworkSetCache.TryGetValue(frameworkListsPath, out result))
                return result;

            result = new FrameworkSet();

            foreach (string fxDir in Directory.EnumerateDirectories(frameworkListsPath))
            {
                string targetName = Path.GetFileName(fxDir);
                Framework framework = new Framework(targetName);
                foreach (string frameworkListPath in Directory.EnumerateFiles(fxDir, "*.xml"))
                {
                    AddAssembliesFromFrameworkList(framework.Assemblies, frameworkListPath);
                }

                SortedSet<Framework> frameworkVersions = null;
                string fxId = framework.FrameworkName.Identifier;

                if (!result.Frameworks.TryGetValue(fxId, out frameworkVersions))
                {
                    frameworkVersions = new SortedSet<Framework>();
                }

                frameworkVersions.Add(framework);

                result.Frameworks[fxId] = frameworkVersions;
            }

            string lastNonSemanticVersionsListPath = Path.Combine(frameworkListsPath, LastNonSemanticVersionsFileName);
            AddAssembliesFromFrameworkList(result.LastNonSemanticVersions, lastNonSemanticVersionsListPath);

            lock (s_frameworkSetCacheLock)
            {
                s_frameworkSetCache[frameworkListsPath] = result;
            }
            return result;
        }

        private static void AddAssembliesFromFrameworkList(IDictionary<string, Version> assemblies, string frameworkListPath)
        {
            XDocument frameworkList = XDocument.Load(frameworkListPath);
            foreach (var file in frameworkList.Element("FileList").Elements("File"))
            {
                string assemblyName = file.Attribute("AssemblyName").Value;
                var versionAttribute = file.Attribute("Version");
                Version supportedVersion = null;

                if (versionAttribute != null)
                {
                    supportedVersion = new Version(versionAttribute.Value);
                }

                // Use a file entry with no version to indicate any version, 
                // this is how Xamarin wishes us to support them
                assemblies.Add(assemblyName,
                    supportedVersion ??
                    new Version(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
            }
        }

        public Dictionary<string, SortedSet<Framework>> Frameworks { get; private set; }

        public Dictionary<string, Version> LastNonSemanticVersions { get; private set; }
    }


    public class Framework : IComparable<Framework>
    {
        public Framework(string targetName)
        {
            Assemblies = new Dictionary<string, Version>();
            FrameworkName = new FrameworkName(targetName);
            var nugetFramework = new NuGetFramework(FrameworkName.Identifier, FrameworkName.Version);
            ShortName = nugetFramework.GetShortFolderName();

            if (ShortName.EndsWith(nugetFramework.Version.Major.ToString()) && nugetFramework.Version.Minor == 0)
            {
                // prefer a trailing zero
                ShortName += "0";
            }

            if (ShortName == "win" || ShortName == "netcore45")
            {
                // prefer the versioned short name
                ShortName = "win8";
            }

            if (ShortName == "netcore451")
            {
                ShortName = "win81";
            }
        }

        public IDictionary<string, Version> Assemblies { get; private set; }
        public FrameworkName FrameworkName { get; private set; }
        public string ShortName { get; private set; }


        public int CompareTo(Framework other)
        {
            if (this.FrameworkName.Identifier != other.FrameworkName.Identifier)
            {
                throw new ArgumentException("Frameworks with different IDs are not comparable.", "other");
            }

            return this.FrameworkName.Version.CompareTo(other.FrameworkName.Version);
        }
    }
    public class Frameworks
    {
        private static FrameworkSet s_inboxFrameworks;
        private static FrameworkSet GetInboxFrameworks(string frameworkListsPath)
        {
            if (s_inboxFrameworks == null)
                s_inboxFrameworks = FrameworkSet.Load(frameworkListsPath);
            return s_inboxFrameworks;
        }
        public static string[] GetInboxFrameworksList(string frameworkListsPath, string assemblyName, string assemblyVersion, ILog log)
        {
            // if no version is specified just use 0.0.0.0 to evaluate for any version of the contract
            Version version = String.IsNullOrEmpty(assemblyVersion) ? new Version(0, 0, 0, 0) : new Version(assemblyVersion);

            FrameworkSet fxs = GetInboxFrameworks(frameworkListsPath);

            Version latestLegacyVersion = null;
            fxs.LastNonSemanticVersions.TryGetValue(assemblyName, out latestLegacyVersion);

            List<string> inboxIds = new List<string>();

            foreach (var fxVersions in fxs.Frameworks.Values)
            {
                // find the first version (if any) that supports this contract
                foreach (var fxVersion in fxVersions)
                {
                    Version supportedVersion;
                    if (fxVersion.Assemblies.TryGetValue(assemblyName, out supportedVersion))
                    {
                        if (supportedVersion >= version)
                        {
                            if (log != null)
                                log.LogMessage(LogImportance.Low, "inbox on {0}", fxVersion.ShortName);
                            inboxIds.Add(fxVersion.ShortName);
                            break;
                        }

                        // new versions represent API surface via major.minor only, so consider
                        // a contract as supported so long as the latest legacy version is supported
                        // and this contract's major.minor match the latest legacy version.
                        if (supportedVersion == latestLegacyVersion &&
                            version.Major == latestLegacyVersion.Major && version.Minor == latestLegacyVersion.Minor)
                        {
                            if (log != null)
                                log.LogMessage(LogImportance.Low, "Considering {0},Version={1} inbox on {2}, since it only differs in revsion.build from {3}", assemblyName, assemblyVersion, fxVersion.ShortName, latestLegacyVersion);
                            inboxIds.Add(fxVersion.ShortName);
                            break;
                        }
                    }
                }
            }
            return inboxIds.ToArray();
        }

        public static bool IsInbox(string frameworkListsPath, string framework, string assemblyName, string assemblyVersion)
        {
            // if no version is specified just use 0.0.0.0 to evaluate for any version of the contract
            Version version = FrameworkUtilities.Ensure4PartVersion(String.IsNullOrEmpty(assemblyVersion) ? new Version(0, 0, 0, 0) : new Version(assemblyVersion));
            FrameworkSet fxs = GetInboxFrameworks(frameworkListsPath);

            Version latestLegacyVersion = null;
            fxs.LastNonSemanticVersions.TryGetValue(assemblyName, out latestLegacyVersion);

            foreach (var fxVersions in fxs.Frameworks.Values)
            {
                // Get the nearest compatible framework from this set of frameworks.
                var nearest = FrameworkUtilities.GetNearest(NuGetFramework.Parse(framework), fxVersions.Select(fx => NuGetFramework.Parse(fx.ShortName)).ToArray());
                // If there are not compatible frameworks in the current framework set, there is not going to be a match.
                if (nearest == null)
                {
                    continue;
                }
                var origFramework = NuGetFramework.Parse(framework);
                // if the nearest compatible frameworks version is greater than the version of the framework we are looking for, this is not going to be a match.
                if (nearest.Version > origFramework.Version)
                {
                    continue;
                }
                // find the first version (if any) that supports this contract
                foreach (var fxVersion in fxVersions)
                {
                    Version supportedVersion;
                    if (fxVersion.Assemblies.TryGetValue(assemblyName, out supportedVersion))
                    {
                        if (supportedVersion >= version)
                        {
                            return true;
                        }

                        // new versions represent API surface via major.minor only, so consider
                        // a contract as supported so long as the latest legacy version is supported
                        // and this contract's major.minor match the latest legacy version.
                        if (supportedVersion == latestLegacyVersion &&
                            version.Major == latestLegacyVersion.Major && version.Minor == latestLegacyVersion.Minor)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        internal static IEnumerable<NuGetFramework> GetAlllInboxFrameworks(string frameworkListsPath)
        {
            FrameworkSet fxs = FrameworkSet.Load(frameworkListsPath);
            return fxs.Frameworks.SelectMany(fxList => fxList.Value).Select(fx => NuGetFramework.Parse(fx.ShortName));
        }
    }
}
