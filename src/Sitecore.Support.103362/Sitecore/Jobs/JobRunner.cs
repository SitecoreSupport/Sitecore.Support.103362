using Sitecore.Data.LanguageFallback;
using Sitecore.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Jobs
{
    public class JobRunner
    {
        public static void EnterLanguageFallbackSwitcher(JobArgs args)
        {
            if (JobRunner.CheckSwitchLanguage(args))
            {
                LanguageFallbackItemSwitcher.Enter(false);
            }
        }

        public static void ExitLanguageFallbackSwitcher(JobArgs args)
        {
            if (JobRunner.CheckSwitchLanguage(args))
            {
                LanguageFallbackItemSwitcher.Exit();
            }
        }

        private static bool CheckSwitchLanguage(JobArgs args)
        {
            return (args.Job.Category == "Install" && args.Job.Name == "Install")
                || (args.Job.Category == "PackageDesigner" && args.Job.Name == "BuildPackage");
        }
    }
}