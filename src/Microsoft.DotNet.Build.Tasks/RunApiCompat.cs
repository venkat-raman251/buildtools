// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ApiCompat;

namespace Microsoft.DotNet.Build.Tasks
{
    public class RunApiCompat : Task
    {
        [Required]
        public string Arguments { get; set; }


        public override bool Execute()
        {
            ApiCompatRunner.ValidateApiCompat(Arguments);
            return true;
        }
    }
}
