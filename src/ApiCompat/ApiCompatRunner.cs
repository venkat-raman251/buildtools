// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Comparers;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;
using Microsoft.Cci.Mappings;
using Microsoft.Cci.Writers;
using Microsoft.Cci.Writers.Syntax;
using Microsoft.Fx.CommandLine;
using System.Reflection;

namespace ApiCompat
{
    public class ExportCciSettings
    {
        public static IEqualityComparer<ITypeReference> StaticSettings { get; set; }
        public ExportCciSettings()
        {
            Settings = StaticSettings;
        }

        [Export(typeof(IEqualityComparer<ITypeReference>))]
        public IEqualityComparer<ITypeReference> Settings { get; set; }
    }

    public static class ApiCompatRunner
    {
        public static int ValidateApiCompat(string contracts, string implDirs, string contractDepends, string baseline, bool listRules,
                string remapFile, bool groupByAssembly, bool unifyToLibPath, string outFile, bool resolveFx,
                string contractCoreAssembly, bool ignoreDesignTimeFacades, bool warnOnIncorrectVersion,
                bool warnOnMissingAssemblies, bool mdil, bool excludeNonBrowsable)
        {
            Console.WriteLine("Nova: This is printing inside apicompatRunner.ValidateApiCompat");
            CommandLineTraceHandler.Enable();
            // Setting all the parameters that have been passed to the task
            s_contractCoreAssembly = contractCoreAssembly;
            s_contractSet = contracts;
            s_implDirs = implDirs;
            s_contractLibDirs = contractDepends;
            s_listRules = listRules;
            s_outFile = outFile;
            s_baselineFileName = baseline;
            s_remapFile = remapFile;
            s_groupByAssembly = groupByAssembly;
            s_mdil = mdil;
            s_resolveFx = resolveFx;
            s_unifyToLibPaths = unifyToLibPath;
            s_warnOnIncorrectVersion = warnOnIncorrectVersion;
            s_ignoreDesignTimeFacades = ignoreDesignTimeFacades;
            s_excludeNonBrowsable = excludeNonBrowsable;
            s_warnOnMissingAssemblies = warnOnMissingAssemblies;

            if (s_listRules)
            {
                CompositionHost c = GetCompositionHost();
                ExportCciSettings.StaticSettings = CciComparers.Default.GetEqualityComparer<ITypeReference>();

                var rules = c.GetExports<IDifferenceRule>();

                foreach (var rule in rules.Select(r => r.GetType().Name).OrderBy(r => r))
                {
                    Console.WriteLine(rule);
                }

                return 0;
            }

            using (TextWriter output = GetOutput())
            {
                if (DifferenceWriter.ExitCode != 0)
                    return 0;

                if (output != Console.Out)
                    Trace.Listeners.Add(new TextWriterTraceListener(output) { Filter = new EventTypeFilter(SourceLevels.Error | SourceLevels.Warning) });
                try
                {
                    BaselineDifferenceFilter filter = GetBaselineDifferenceFilter();
                    NameTable sharedNameTable = new NameTable();
                    HostEnvironment contractHost = new HostEnvironment(sharedNameTable);
                    contractHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(contractHost_UnableToResolve);
                    contractHost.ResolveAgainstRunningFramework = s_resolveFx;
                    contractHost.UnifyToLibPath = s_unifyToLibPaths;
                    contractHost.AddLibPaths(HostEnvironment.SplitPaths(s_contractLibDirs));
                    IEnumerable<IAssembly> contractAssemblies = contractHost.LoadAssemblies(s_contractSet, s_contractCoreAssembly);

                    if (s_ignoreDesignTimeFacades)
                        contractAssemblies = contractAssemblies.Where(a => !a.IsFacade());

                    HostEnvironment implHost = new HostEnvironment(sharedNameTable);
                    implHost.UnableToResolve += new EventHandler<UnresolvedReference<IUnit, AssemblyIdentity>>(implHost_UnableToResolve);
                    implHost.ResolveAgainstRunningFramework = s_resolveFx;
                    implHost.UnifyToLibPath = s_unifyToLibPaths;
                    implHost.AddLibPaths(HostEnvironment.SplitPaths(s_implDirs));
                    if (s_warnOnMissingAssemblies)
                        implHost.LoadErrorTreatment = ErrorTreatment.TreatAsWarning;

                    // The list of contractAssemblies already has the core assembly as the first one (if _contractCoreAssembly was specified).
                    IEnumerable<IAssembly> implAssemblies = implHost.LoadAssemblies(contractAssemblies.Select(a => a.AssemblyIdentity), s_warnOnIncorrectVersion);
                    Console.WriteLine("Nova: This is printing inside apicompatRunner.ValidateApiCompat. Before The exitcode");
                    // Exit after loading if the code is set to non-zero
                    if (DifferenceWriter.ExitCode != 0)
                        return 0;
                    Console.WriteLine("Nova: This is printing inside apicompatRunner.ValidateApiCompat. Before GetDiffWriter");
                    ICciDifferenceWriter writer = GetDifferenceWriter(output, filter);
                    writer.Write(s_implDirs, implAssemblies, s_contractSet, contractAssemblies);
                    return 0;
                }
                catch (FileNotFoundException)
                {
                    // FileNotFoundException will be thrown by GetBaselineDifferenceFilter if it doesn't find the baseline file
                    // OR if GetComparers doesn't find the remap file.
                    Console.WriteLine("Nova: This is printing inside FileNotFoundException");
                    return 2;
                }
            }
        }

        private static BaselineDifferenceFilter GetBaselineDifferenceFilter()
        {
            BaselineDifferenceFilter filter = null;
            if (!string.IsNullOrEmpty(s_baselineFileName))
            {
                if (!File.Exists(s_baselineFileName))
                {
                    throw new FileNotFoundException("Baseline file {0} was not found!", s_baselineFileName);
                }
                IDifferenceFilter incompatibleFilter = new DifferenceFilter<IncompatibleDifference>();
                filter = new BaselineDifferenceFilter(incompatibleFilter, s_baselineFileName);
            }
            return filter;
        }

        private static void implHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError("Unable to resolve assembly '{0}' referenced by the implementation assembly '{1}'.", e.Unresolved, e.Referrer);
        }

        private static void contractHost_UnableToResolve(object sender, UnresolvedReference<IUnit, AssemblyIdentity> e)
        {
            Trace.TraceError("Unable to resolve assembly '{0}' referenced by the contract assembly '{1}'.", e.Unresolved, e.Referrer);
        }

        private static TextWriter GetOutput()
        {
            if (string.IsNullOrWhiteSpace(s_outFile))
                return Console.Out;

            const int NumRetries = 10;
            String exceptionMessage = null;
            for (int retries = 0; retries < NumRetries; retries++)
            {
                try
                {
                    return new StreamWriter(File.OpenWrite(s_outFile));
                }
                catch (Exception e)
                {
                    exceptionMessage = e.Message;
                    System.Threading.Thread.Sleep(100);
                }
            }

            Trace.TraceError("Cannot open output file '{0}': {1}", s_outFile, exceptionMessage);
            return Console.Out;
        }

        private static ICciDifferenceWriter GetDifferenceWriter(TextWriter writer, IDifferenceFilter filter)
        {
            Console.WriteLine("Nova: This is printing inside apicompatRunner.ValidateApiCompat. Inside GetDiffWriter");
            CompositionHost container = GetCompositionHost();

            Func<IDifferenceRuleMetadata, bool> ruleFilter =
                delegate (IDifferenceRuleMetadata ruleMetadata)
                {
                    if (ruleMetadata.MdilServicingRule && !s_mdil)
                        return false;
                    return true;
                };

            if (s_mdil && s_excludeNonBrowsable)
            {
                Trace.TraceWarning("Enforcing MDIL servicing rules and exclusion of non-browsable types are both enabled, but they are not compatible so non-browsable types will not be excluded.");
            }

            MappingSettings settings = new MappingSettings();
            settings.Comparers = GetComparers();
            settings.Filter = GetCciFilter(s_mdil, s_excludeNonBrowsable);
            settings.DiffFilter = GetDiffFilter(settings.Filter);
            settings.DiffFactory = new ElementDifferenceFactory(container, ruleFilter);
            settings.GroupByAssembly = s_groupByAssembly;
            settings.IncludeForwardedTypes = true;

            if (filter == null)
            {
                filter = new DifferenceFilter<IncompatibleDifference>();
            }
            Console.WriteLine("Nova: This is printing inside apicompatRunner.ValidateApiCompat. Before new DifferenceWriter");
            ICciDifferenceWriter diffWriter = new DifferenceWriter(writer, settings, filter);
            ExportCciSettings.StaticSettings = settings.TypeComparer;

            // Always compose the diff writer to allow it to import or provide exports
            container.SatisfyImports(diffWriter);

            return diffWriter;
        }

        private static CompositionHost GetCompositionHost()
        {
            var configuration = new ContainerConfiguration().WithAssembly(typeof(ApiCompatRunner).GetTypeInfo().Assembly);
            return configuration.CreateContainer();
        }

        private static ICciComparers GetComparers()
        {
            if (!string.IsNullOrEmpty(s_remapFile))
            {
                if (!File.Exists(s_remapFile))
                {
                    throw new FileNotFoundException("ERROR: RemapFile {0} was not found!", s_remapFile);
                }
                return new NamespaceRemappingComparers(s_remapFile);
            }
            return CciComparers.Default;
        }

        private static ICciFilter GetCciFilter(bool enforcingMdilRules, bool excludeNonBrowsable)
        {
            if (enforcingMdilRules)
            {
                return new MdilPublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else if (excludeNonBrowsable)
            {
                return new PublicEditorBrowsableOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
            else
            {
                return new PublicOnlyCciFilter()
                {
                    IncludeForwardedTypes = true
                };
            }
        }

        private static IMappingDifferenceFilter GetDiffFilter(ICciFilter filter)
        {
            return new MappingDifferenceFilter(GetIncludeFilter(), filter);
        }

        private static Func<DifferenceType, bool> GetIncludeFilter()
        {
            return d => d != DifferenceType.Unchanged;
        }

        private static string s_contractCoreAssembly;
        private static string s_contractSet;
        private static string s_implDirs;
        private static string s_contractLibDirs;
        private static bool s_listRules;
        private static string s_outFile;
        private static string s_baselineFileName;
        private static string s_remapFile;
        private static bool s_groupByAssembly = true;
        private static bool s_mdil;
        private static bool s_resolveFx;
        private static bool s_unifyToLibPaths = true;
        private static bool s_warnOnIncorrectVersion;
        private static bool s_ignoreDesignTimeFacades;
        private static bool s_excludeNonBrowsable;
        private static bool s_warnOnMissingAssemblies;
    }
}
