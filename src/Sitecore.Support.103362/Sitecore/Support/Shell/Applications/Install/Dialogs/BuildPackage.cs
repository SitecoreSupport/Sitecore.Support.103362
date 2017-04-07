namespace Sitecore.Support.Shell.Applications.Install.Dialogs
{
    using Sitecore;
    using Sitecore.Data.LanguageFallback;
    using Sitecore.Data.Proxies;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Install;
    using Sitecore.Install.Framework;
    using Sitecore.Install.Serialization;
    using Sitecore.IO;
    using Sitecore.Jobs.AsyncUI;
    using Sitecore.Shell.Applications.Install;
    using Sitecore.Web.UI.HtmlControls;
    using Sitecore.Web.UI.Pages;
    using Sitecore.Web.UI.Sheer;
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;

    public class BuildPackage : WizardForm
    {
        protected Border FailureMessage;
        protected JobMonitor Monitor;
        protected Edit PackageFile;
        protected Border SuccessMessage;

        protected override void ActivePageChanged(string page, string oldPage)
        {
            base.ActivePageChanged(page, oldPage);
            if (page == "Building")
            {
                base.BackButton.Disabled = true;
                base.NextButton.Disabled = true;
                base.CancelButton.Disabled = true;
                Context.ClientPage.SendMessage(this, "buildpackage:generate");
            }
            if (page == "LastPage")
            {
                base.BackButton.Disabled = true;
            }
        }

        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            if ((page == "SetName") && (newpage == "Building"))
            {
                string fullPackagePath;
                if ((this.PackageFile.Value.Trim().Length == 0) || (this.PackageFile.Value.IndexOfAny(Path.GetInvalidPathChars()) != -1))
                {
                    Context.ClientPage.ClientResponse.Alert(Translate.Text("Enter a valid name for the package."));
                    Context.ClientPage.ClientResponse.Focus(this.PackageFile.ID);
                    return false;
                }
                try
                {
                    fullPackagePath = ApplicationContext.GetFullPackagePath(Installer.GetFilename(this.PackageFile.Value.Trim()));
                    Path.GetDirectoryName(fullPackagePath);
                }
                catch (Exception exception)
                {
                    Log.Error("Noncritical: " + exception.ToString(), this);
                    Context.ClientPage.ClientResponse.Alert(Translate.Text("Entered name could not be resolved into an absolute file path.") + Environment.NewLine + Translate.Text("Enter a valid name for the package."));
                    Context.ClientPage.ClientResponse.Focus(this.PackageFile.ID);
                    return false;
                }
                if (File.Exists(fullPackagePath) && !MainUtil.GetBool(Context.ClientPage.ServerProperties["__NameConfirmed"], false))
                {
                    Context.ClientPage.Start(this, "AskOverwrite");
                    return false;
                }
                Context.ClientPage.ServerProperties.Remove("__NameConfirmed");
            }
            return base.ActivePageChanging(page, ref newpage);
        }

        public void AskOverwrite(ClientPipelineArgs args)
        {
            if (!args.IsPostBack)
            {
                Context.ClientPage.ClientResponse.Confirm(Translate.Text("File exists. Do you wish to overwrite?"));
                args.WaitForPostBack();
            }
            else if (args.HasResult && (args.Result == "yes"))
            {
                Context.ClientPage.ClientResponse.SetDialogValue(args.Result);
                Context.ClientPage.ServerProperties["__NameConfirmed"] = true;
                base.Next();
            }
        }

        [HandleMessage("buildpackage:download")]
        protected void DownloadPackage(Message message)
        {
            string resultFile = this.ResultFile;
            if (resultFile.Length > 0)
            {
                Context.ClientPage.ClientResponse.Download(resultFile);
            }
            else
            {
                Context.ClientPage.ClientResponse.Alert("Could not download package");
            }
        }

        private string GeneratePackageFileName(PackageProject project)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(PackageUtils.CleanupFileName(project.Metadata.PackageName));
            if (project.Metadata.Version.Length > 0)
            {
                builder.Append("-");
                builder.Append(project.Metadata.Version);
            }
            if (builder.Length == 0)
            {
                builder.Append(Sitecore.Install.Constants.UnnamedPackage);
            }
            builder.Append(".zip");
            return builder.ToString();
        }

        private void Monitor_Finished(object sender, EventArgs e)
        {
            base.Next();
        }

        [HandleMessage("build:failed")]
        protected void OnBuildFailed(Message message)
        {
            string[] values = new string[] { message["message"] };
            string str = StringUtil.GetString(values);
            Context.ClientPage.ClientResponse.SetStyle("SuccessMessage", "display", "none");
            Context.ClientPage.ClientResponse.SetStyle("FailureMessage", "display", "");
            object[] parameters = new object[] { str };
            Context.ClientPage.ClientResponse.SetInnerHtml("FailureMessage", Translate.Text("Package generation failed: {0}.", parameters));
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!Context.ClientPage.IsEvent)
            {
                string[] values = new string[] { Context.Request.QueryString["source"] };
                this.FileName = StringUtil.GetString(values);
                PackageProject project = IOUtils.LoadSolution(FileUtil.ReadFromFile(MainUtil.MapPath(this.FileName)));
                if (project != null)
                {
                    this.PackageFile.Value = this.GeneratePackageFileName(project);
                }
                if (this.Monitor == null)
                {
                    this.Monitor = new JobMonitor();
                    this.Monitor.ID = "Monitor";
                    Context.ClientPage.Controls.Add(this.Monitor);
                }
            }
            else if (this.Monitor == null)
            {
                this.Monitor = Context.ClientPage.FindControl("Monitor") as JobMonitor;
            }
            this.Monitor.JobFinished += new EventHandler(this.Monitor_Finished);
            this.Monitor.JobDisappeared += new EventHandler(this.Monitor_Finished);
        }

        [HandleMessage("buildpackage:generate")]
        protected void StartPackage(Message message)
        {
            string filename = Installer.GetFilename(this.PackageFile.Value);
            if (string.Compare(Path.GetExtension(filename), Sitecore.Install.Constants.PackageExtension, StringComparison.InvariantCultureIgnoreCase) != 0)
            {
                filename = filename + Sitecore.Install.Constants.PackageExtension;
            }
            this.ResultFile = filename;
            this.StartTask(this.FileName, filename);
        }

        private void StartTask(string solutionFile, string packageFile)
        {
            this.Monitor.Start("BuildPackage", "PackageDesigner", new ThreadStart(new AsyncHelper(solutionFile, packageFile).Generate));
        }

      public string FileName
      {
        get { return StringUtil.GetString(Context.ClientPage.ServerProperties["FileName"]); }
        set { Context.ClientPage.ServerProperties["FileName"] = value; }
      }

      public string ResultFile
      {
        get { return StringUtil.GetString(Context.ClientPage.ServerProperties["ResultFile"]); }
        set { Context.ClientPage.ServerProperties["ResultFile"] = value; }
      }

      [Serializable]
        private class AsyncHelper
        {
            private string packageFile;
            private string solutionFile;

            public AsyncHelper(string solutionFile, string packageFile)
            {
                this.solutionFile = solutionFile;
                this.packageFile = packageFile;
            }

            public void Generate()
            {
                try
                {
                    using (new ProxyDisabler())
                    {
                        using (new LanguageFallbackItemSwitcher(false))
                        {
                            PackageGenerator.GeneratePackage(this.solutionFile, this.packageFile, new SimpleProcessingContext());
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.Error("Package generation failed: " + exception.ToString(), this);
                    JobContext.SendMessage("build:failed(message=" + exception.Message + ")");
                    JobContext.Flush();
                    throw exception;
                }
            }
        }
    }
}

