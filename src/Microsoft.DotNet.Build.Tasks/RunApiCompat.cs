// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ApiCompat;
using System;

namespace Microsoft.DotNet.Build.Tasks
{
    public class RunApiCompat : Task
    {
        [Required]
        public string Contracts { get; set; }

        [Required]
        public string ImplDirs { get; set; }

        public string ContractDepends { get; set; }

        public string Baseline { get; set; }

        public bool ListRules { get; set; }

        public string RemapFile { get; set; }

        public bool GroupByAssembly { get; set; }

        public bool UnifyToLibPath { get; set; }

        public string OutFile { get; set; }

        public bool ResolveFx { get; set; }

        public string ContractCoreAssembly { get; set; }

        public bool IgnoreDesignTimeFacades { get; set; }

        public bool WarnOnIncorrectVersion { get; set; }

        public bool WarnOnMissingAssemblies { get; set; }

        public bool Mdil { get; set; }

        public bool ExcludeNonBrowsable { get; set; }




        public override bool Execute()
        {
            int returnValue = ApiCompatRunner.ValidateApiCompat(Contracts, ImplDirs, ContractDepends, Baseline, ListRules, RemapFile, GroupByAssembly,
                UnifyToLibPath, OutFile, ResolveFx, ContractCoreAssembly, IgnoreDesignTimeFacades, WarnOnIncorrectVersion,
                WarnOnMissingAssemblies, Mdil, ExcludeNonBrowsable);
            Console.Out.WriteLine("Nova : returnValue in runapicompat : " + returnValue);
            if(returnValue != 0)
            {
                return false;
            }
            else
            {
                return true;
            }                
        }
    }
}
