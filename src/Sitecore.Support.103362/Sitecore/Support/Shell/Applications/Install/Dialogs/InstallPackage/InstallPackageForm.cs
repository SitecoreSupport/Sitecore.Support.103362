using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Engines;
using Sitecore.Data.LanguageFallback;
using Sitecore.Data.Proxies;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Globalization;
using Sitecore.Install;
using Sitecore.Install.Events;
using Sitecore.Install.Files;
using Sitecore.Install.Framework;
using Sitecore.Install.Items;
using Sitecore.Install.Metadata;
using Sitecore.Install.Security;
using Sitecore.Install.Utils;
using Sitecore.Install.Zip;
using Sitecore.IO;
using Sitecore.Jobs;
using Sitecore.Jobs.AsyncUI;
using Sitecore.SecurityModel;
using Sitecore.Shell.Framework;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Sitecore.Support.Shell.Applications.Install.Dialogs.InstallPackage
{
    public class InstallPackageForm : WizardForm
    {
        private class AsyncHelper
        {
            private string _packageFile;

            private string _postAction;

            private IProcessingContext _context;

            private StatusFile _statusFile;

            private Language _language;

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            /// <param name="package">The package.</param>
            public AsyncHelper(string package)
            {
                this._packageFile = package;
                this._language = Context.Language;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            /// <param name="postAction">The post action.</param>
            /// <param name="context">The context.</param>
            public AsyncHelper(string postAction, IProcessingContext context)
            {
                this._postAction = postAction;
                this._context = context;
                this._language = Context.Language;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="T:Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.InstallPackageForm.AsyncHelper" /> class.
            /// </summary>
            public AsyncHelper()
            {
                this._language = Context.Language;
            }

            /// <summary>
            /// Performs installation.
            /// </summary>
            public void Install()
            {
                this.CatchExceptions(delegate
                {
                    using (new SecurityDisabler())
                    using (new ProxyDisabler())
                    using (new SyncOperationContext())
                    using (new LanguageSwitcher(this._language))
                    using (VirtualDrive virtualDrive = new VirtualDrive(FileUtil.MapPath(Settings.TempFolderPath)))
                    using (new LanguageFallbackItemSwitcher(new bool?(false)))
                    {
                        SettingsSwitcher settingsSwitcher = null;
                        try
                        {
                            if (!string.IsNullOrEmpty(virtualDrive.Name))
                            {
                                settingsSwitcher = new SettingsSwitcher("TempFolder", virtualDrive.Name);
                            }
                            IProcessingContext processingContext = Installer.CreateInstallationContext();
                            JobContext.PostMessage("installer:setTaskId(id=" + processingContext.TaskID + ")");
                            processingContext.AddAspect<IItemInstallerEvents>(
                                Activator.CreateInstance(
                                    Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.UiInstallerEvents, Sitecore.Client"),
                                    BindingFlags.CreateInstance,
                                    null,
                                    new object[] { },
                                    null) as IItemInstallerEvents);
                            processingContext.AddAspect<IFileInstallerEvents>(
                                Activator.CreateInstance(
                                    Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.UiInstallerEvents, Sitecore.Client"),
                                    BindingFlags.CreateInstance,
                                    null,
                                    new object[] { },
                                    null) as IFileInstallerEvents);
                            Installer installer = new Installer();
                            installer.InstallPackage(PathUtils.MapPath(this._packageFile), processingContext);
                        }
                        finally
                        {
                            if (settingsSwitcher != null)
                            {
                                settingsSwitcher.Dispose();
                            }
                        }
                    }
                });
            }

            /// <summary>
            /// Installs the security.
            /// </summary>
            public void InstallSecurity()
            {
                this.CatchExceptions(delegate
                {
                    using (new LanguageSwitcher(this._language))
                    {
                        IProcessingContext processingContext = Installer.CreateInstallationContext();
                        processingContext.AddAspect<IAccountInstallerEvents>(
                            Activator.CreateInstance(
                                    Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.InstallPackage.UiInstallerEvents, Sitecore.Client"),
                                    BindingFlags.CreateInstance,
                                    null,
                                    new object[] { },
                                    null) as IAccountInstallerEvents);
                        Installer installer = new Installer();
                        installer.InstallSecurity(PathUtils.MapPath(this._packageFile), processingContext);
                    }
                });
            }

            /// <summary>
            /// Sets the status file.
            /// </summary>
            /// <param name="filename">The filename.</param>
            /// <returns>The status file.</returns>
            public InstallPackageForm.AsyncHelper SetStatusFile(string filename)
            {
                this._statusFile = new StatusFile(filename);
                return this;
            }

            /// <summary>
            /// Watches for status.
            /// </summary>
            /// <exception cref="T:System.Exception"><c>Exception</c>.</exception>
            public void WatchForStatus()
            {
                this.CatchExceptions(delegate
                {
                    Assert.IsNotNull(this._statusFile, "Internal error: status file not set.");
                    bool flag = false;
                    StatusFile.StatusInfo statusInfo;
                    while (true)
                    {
                        statusInfo = this._statusFile.ReadStatus();
                        if (statusInfo != null)
                        {
                            switch (statusInfo.Status)
                            {
                                case StatusFile.Status.Finished:
                                    flag = true;
                                    break;
                                case StatusFile.Status.Failed:
                                    goto IL_3E;
                            }
                            System.Threading.Thread.Sleep(100);
                        }
                        if (flag)
                        {
                            return;
                        }
                    }
                    IL_3E:
                    throw new System.Exception("Background process failed: " + statusInfo.Exception.Message, statusInfo.Exception);
                });
            }

            /// <summary></summary>
            public void ExecutePostStep()
            {
                this.CatchExceptions(delegate
                {
                    Installer installer = new Installer();
                    installer.ExecutePostStep(this._postAction, this._context);
                });
            }

            private void CatchExceptions(System.Threading.ThreadStart start)
            {
                try
                {
                    start();
                }
                catch (System.Threading.ThreadAbortException)
                {
                    if (!System.Environment.HasShutdownStarted)
                    {
                        System.Threading.Thread.ResetAbort();
                    }
                    Log.Info("Installation was aborted", this);
                    JobContext.PostMessage("installer:aborted");
                    JobContext.Flush();
                }
                catch (System.Exception ex)
                {
                    Log.Error("Installation failed: " + ex, this);
                    JobContext.Job.Status.Result = ex;
                    JobContext.PostMessage("installer:failed");
                    JobContext.Flush();
                }
            }
        }

        private enum InstallationSteps
        {
            MainInstallation,
            WaitForFiles,
            InstallSecurity,
            RunPostAction,
            None,
            Failed
        }

        private enum Result
        {
            Success,
            Failure,
            Abort
        }

        /// <summary></summary>
        protected Edit PackageFile;

        /// <summary></summary>
        protected Edit PackageName;

        /// <summary></summary>
        protected Edit Version;

        /// <summary></summary>
        protected Edit Author;

        /// <summary></summary>
        protected Edit Publisher;

        /// <summary></summary>
        protected Border LicenseAgreement;

        /// <summary></summary>
        protected Memo ReadmeText;

        /// <summary></summary>
        protected Radiobutton Decline;

        /// <summary></summary>
        protected Radiobutton Accept;

        /// <summary></summary>
        protected Checkbox Restart;

        /// <summary></summary>
        protected Checkbox RestartServer;

        /// <summary></summary>
        protected JobMonitor Monitor;

        /// <summary></summary>
        protected Literal FailingReason;

        /// <summary></summary>
        protected Literal ErrorDescription;

        /// <summary></summary>
        protected Border SuccessMessage;

        /// <summary></summary>
        protected Border ErrorMessage;

        /// <summary></summary>
        protected Border AbortMessage;

        /// <summary>
        /// Synchronization object for current step
        /// </summary>
        private readonly object CurrentStepSync = new object();

        /// <summary>
        /// Gets or sets a value indicating whether this instance has license.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has license; otherwise, <c>false</c>.
        /// </value>
        public bool HasLicense
        {
            get
            {
                return MainUtil.GetBool(Context.ClientPage.ServerProperties["HasLicense"], false);
            }
            set
            {
                Context.ClientPage.ServerProperties["HasLicense"] = value.ToString();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance has readme.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has readme; otherwise, <c>false</c>.
        /// </value>
        public bool HasReadme
        {
            get
            {
                return MainUtil.GetBool(Context.ClientPage.ServerProperties["Readme"], false);
            }
            set
            {
                Context.ClientPage.ServerProperties["Readme"] = value.ToString();
            }
        }

        /// <summary>
        /// Gets or sets the post action.
        /// </summary>
        /// <value>The post action.</value>
        private string PostAction
        {
            get
            {
                return StringUtil.GetString(base.ServerProperties["postAction"]);
            }
            set
            {
                base.ServerProperties["postAction"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the installation step. 0 means installing items and files, 1 means installing security accounts.
        /// </summary>
        /// <value>The installation step.</value>
        private InstallPackageForm.InstallationSteps CurrentStep
        {
            get
            {
                return (InstallPackageForm.InstallationSteps)((int)base.ServerProperties["installationStep"]);
            }
            set
            {
                lock (this.CurrentStepSync)
                {
                    base.ServerProperties["installationStep"] = (int)value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        /// <value>The package version.</value>
        private int PackageVersion
        {
            get
            {
                return int.Parse(StringUtil.GetString(base.ServerProperties["packageType"], "1"));
            }
            set
            {
                base.ServerProperties["packageType"] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this installation is successful.
        /// </summary>
        /// <value><c>true</c> if successful; otherwise, <c>false</c>.</value>
        private bool Successful
        {
            get
            {
                object obj = base.ServerProperties["Successful"];
                return !(obj is bool) || (bool)obj;
            }
            set
            {
                base.ServerProperties["Successful"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the main installation task ID.
        /// </summary>
        /// <value>The main installation task ID.</value>
        private string MainInstallationTaskID
        {
            get
            {
                return StringUtil.GetString(base.ServerProperties["taskID"]);
            }
            set
            {
                base.ServerProperties["taskID"] = value;
            }
        }

        private bool Cancelling
        {
            get
            {
                return MainUtil.GetBool(Context.ClientPage.ServerProperties["__cancelling"], false);
            }
            set
            {
                Context.ClientPage.ServerProperties["__cancelling"] = value;
            }
        }

        private string OriginalNextButtonHeader
        {
            get
            {
                return StringUtil.GetString(Context.ClientPage.ServerProperties["next-header"]);
            }
            set
            {
                Context.ClientPage.ServerProperties["next-header"] = value;
            }
        }

        /// <summary></summary>
        protected override void OnLoad(System.EventArgs e)
        {
            if (!Context.ClientPage.IsEvent)
            {
                this.OriginalNextButtonHeader = this.NextButton.Header;
            }
            base.OnLoad(e);
            this.Monitor = Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils, Sitecore.Client")
                .GetMethod("AttachMonitor", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { this.Monitor }) as JobMonitor;
            if (!Context.ClientPage.IsEvent)
            {
                this.PackageFile.Value = Registry.GetString("Packager/File");
                this.Decline.Checked = true;
                this.Restart.Checked = true;
                this.RestartServer.Checked = false;
            }
            this.Monitor.JobFinished += new System.EventHandler(this.Monitor_JobFinished);
            this.Monitor.JobDisappeared += new System.EventHandler(this.Monitor_JobDisappeared);
            base.WizardCloseConfirmationText = "Are you sure you want to cancel installing a package.";
        }

        /// <summary>
        /// Called when the active page is changing.
        /// </summary>
        /// <param name="page">The page that is being left.</param>
        /// <param name="newpage">The new page that is being entered.</param>
        protected override bool ActivePageChanging(string page, ref string newpage)
        {
            bool result = base.ActivePageChanging(page, ref newpage);
            if (page == "LoadPackage" && newpage == "License")
            {
                result = this.LoadPackage();
                if (!this.HasLicense)
                {
                    newpage = "Readme";
                    if (!this.HasReadme)
                    {
                        newpage = "Ready";
                    }
                }
                return result;
            }
            if (page == "License" && newpage == "Readme")
            {
                if (!this.HasReadme)
                {
                    newpage = "Ready";
                }
                return result;
            }
            if (page == "Ready" && newpage == "Readme")
            {
                if (!this.HasReadme)
                {
                    newpage = "License";
                    if (!this.HasLicense)
                    {
                        newpage = "LoadPackage";
                    }
                }
                return result;
            }
            if (page == "Readme" && newpage == "License" && !this.HasLicense)
            {
                newpage = "LoadPackage";
            }
            return result;
        }

        /// <summary>
        /// Called when the active page has been changed.
        /// </summary>
        /// <param name="page">The page that has been entered.</param>
        /// <param name="oldPage">The page that was left.</param>
        protected override void ActivePageChanged(string page, string oldPage)
        {
            base.ActivePageChanged(page, oldPage);
            this.NextButton.Header = this.OriginalNextButtonHeader;
            if (page == "License" && oldPage == "LoadPackage")
            {
                this.NextButton.Disabled = !this.Accept.Checked;
            }
            if (page == "Installing")
            {
                this.BackButton.Disabled = true;
                this.NextButton.Disabled = true;
                this.CancelButton.Disabled = true;
                Context.ClientPage.SendMessage(this, "installer:startInstallation");
            }
            if (page == "Ready")
            {
                this.NextButton.Header = Translate.Text("Install");
            }
            if (page == "LastPage")
            {
                this.BackButton.Disabled = true;
            }
            if (!this.Successful)
            {
                this.CancelButton.Header = Translate.Text("Close");
                this.Successful = true;
            }
        }

        /// <summary></summary>
        protected override void EndWizard()
        {
            if (!this.Cancelling)
            {
                if (this.RestartServer.Checked)
                {
                    Installer.RestartServer();
                }
                if (this.Restart.Checked)
                {
                    Context.ClientPage.ClientResponse.Broadcast(Context.ClientPage.ClientResponse.SetLocation(string.Empty), "Shell");
                }
            }
            Windows.Close();
        }

        /// <summary></summary>
        protected override void OnCancel(object sender, System.EventArgs formEventArgs)
        {
            this.Cancel();
        }

        /// <summary>
        /// On "Cancel" click
        /// </summary>
        public new void Cancel()
        {
            int num = base.Pages.IndexOf(base.Active);
            if (num == 0 || num == base.Pages.Count - 1)
            {
                this.Cancelling = (num == 0);
                this.EndWizard();
                return;
            }
            this.Cancelling = true;
            Context.ClientPage.Start(this, "Confirmation");
        }

        /// <summary></summary>
        protected void Done()
        {
            base.Active = "LastPage";
            this.BackButton.Disabled = true;
            this.NextButton.Disabled = true;
            this.CancelButton.Disabled = false;
        }

        /// <summary>
        /// Starts the installation.
        /// </summary>
        /// <param name="message">The message.</param>
        [HandleMessage("installer:startInstallation")]
        protected void StartInstallation(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            this.CurrentStep = InstallPackageForm.InstallationSteps.MainInstallation;
            string filename = Installer.GetFilename(this.PackageFile.Value);
            if (FileUtil.IsFile(filename))
            {
                this.StartTask(filename);
                return;
            }
            Context.ClientPage.ClientResponse.Alert("Package not found");
            base.Active = "Ready";
            this.BackButton.Disabled = true;
        }

        /// <summary>
        /// Sets the task ID.
        /// </summary>
        /// <param name="message">The message.</param>
        [HandleMessage("installer:setTaskId")]
        private void SetTaskID(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.IsNotNull(message["id"], "id");
            this.MainInstallationTaskID = message["id"];
        }

        [HandleMessage("installer:commitingFiles")]
        private void OnCommittingFiles(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            lock (this.CurrentStepSync)
            {
                if (this.CurrentStep == InstallPackageForm.InstallationSteps.MainInstallation)
                {
                    this.CurrentStep = InstallPackageForm.InstallationSteps.WaitForFiles;
                    this.WatchForInstallationStatus();
                }
            }
        }

        private void Monitor_JobFinished(object sender, System.EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(e, "e");
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallPackageForm.InstallationSteps.MainInstallation:
                        this.CurrentStep = InstallPackageForm.InstallationSteps.WaitForFiles;
                        this.WatchForInstallationStatus();
                        break;
                    case InstallPackageForm.InstallationSteps.WaitForFiles:
                        this.CurrentStep = InstallPackageForm.InstallationSteps.InstallSecurity;
                        this.StartInstallingSecurity();
                        break;
                    case InstallPackageForm.InstallationSteps.InstallSecurity:
                        this.CurrentStep = InstallPackageForm.InstallationSteps.RunPostAction;
                        if (string.IsNullOrEmpty(this.PostAction))
                        {
                            this.GotoLastPage(InstallPackageForm.Result.Success, string.Empty, string.Empty);
                        }
                        else
                        {
                            this.StartPostAction();
                        }
                        break;
                    case InstallPackageForm.InstallationSteps.RunPostAction:
                        this.GotoLastPage(InstallPackageForm.Result.Success, string.Empty, string.Empty);
                        break;
                }
            }
        }

        private void Monitor_JobDisappeared(object sender, System.EventArgs e)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(e, "e");
            lock (this.CurrentStepSync)
            {
                switch (this.CurrentStep)
                {
                    case InstallPackageForm.InstallationSteps.MainInstallation:
                        this.GotoLastPage(InstallPackageForm.Result.Failure, Translate.Text("Installation could not be completed."), Translate.Text("Installation job was interrupted unexpectedly."));
                        break;
                    case InstallPackageForm.InstallationSteps.WaitForFiles:
                        this.WatchForInstallationStatus();
                        break;
                    default:
                        this.Monitor_JobFinished(sender, e);
                        break;
                }
            }
        }

        /// <summary></summary>
        [HandleMessage("installer:browse", true)]
        protected void Browse(ClientPipelineArgs args)
        {
            Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils, Sitecore.Client")
                .GetMethod("Browse", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { args, this.PackageFile });
        }

        /// <summary></summary>
        [HandleMessage("installer:upload", true)]
        protected void Upload(ClientPipelineArgs args)
        {
            Type.GetType("Sitecore.Shell.Applications.Install.Dialogs.DialogUtils, Sitecore.Client")
                .GetMethod("Upload", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { args, this.PackageFile });
        }

        /// <summary></summary>
        [HandleMessage("installer:savePostAction")]
        protected void SavePostAction(Message msg)
        {
            string postAction = msg.Arguments[0];
            this.PostAction = postAction;
        }

        /// <summary></summary>
        [HandleMessage("installer:doPostAction")]
        protected void DoPostAction(Message msg)
        {
            string postAction = this.PostAction;
            if (!string.IsNullOrEmpty(postAction))
            {
                this.StartPostAction();
            }
        }

        /// <summary></summary>
        [HandleMessage("installer:aborted")]
        protected void OnInstallerAborted(Message message)
        {
            this.GotoLastPage(InstallPackageForm.Result.Abort, string.Empty, string.Empty);
            this.CurrentStep = InstallPackageForm.InstallationSteps.Failed;
        }

        /// <summary></summary>
        [HandleMessage("installer:failed")]
        protected void OnInstallerFailed(Message message)
        {
            Job job = JobManager.GetJob(this.Monitor.JobHandle);
            Assert.IsNotNull(job, "Job is not available");
            System.Exception ex = job.Status.Result as System.Exception;
            Error.AssertNotNull(ex, "Cannot get any exception details");
            this.GotoLastPage(InstallPackageForm.Result.Failure, InstallPackageForm.GetShortDescription(ex), InstallPackageForm.GetFullDescription(ex));
            this.CurrentStep = InstallPackageForm.InstallationSteps.Failed;
        }

        private void GotoLastPage(InstallPackageForm.Result result, string shortDescription, string fullDescription)
        {
            this.ErrorDescription.Text = fullDescription;
            this.FailingReason.Text = shortDescription;
            this.Cancelling = (result != InstallPackageForm.Result.Success);
            InstallPackageForm.SetVisibility(this.SuccessMessage, result == InstallPackageForm.Result.Success);
            InstallPackageForm.SetVisibility(this.ErrorMessage, result == InstallPackageForm.Result.Failure);
            InstallPackageForm.SetVisibility(this.AbortMessage, result == InstallPackageForm.Result.Abort);
            InstallationEventArgs installationEventArgs = new InstallationEventArgs(new System.Collections.Generic.List<ItemUri>(), new System.Collections.Generic.List<FileCopyInfo>(), "packageinstall:ended");
            Event.RaiseEvent("packageinstall:ended", new object[]
            {
                installationEventArgs
            });
            this.Successful = (result == InstallPackageForm.Result.Success);
            base.Active = "LastPage";
        }

        /// <summary></summary>
        protected void Agree()
        {
            this.NextButton.Disabled = false;
            Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        /// <summary></summary>
        protected void Disagree()
        {
            this.NextButton.Disabled = true;
            Context.ClientPage.ClientResponse.SetReturnValue(true);
        }

        /// <summary></summary>
        protected void RestartInstallation()
        {
            base.Active = "Ready";
            this.CancelButton.Visible = true;
            this.CancelButton.Disabled = false;
            this.NextButton.Visible = true;
            this.NextButton.Disabled = false;
            this.BackButton.Visible = false;
        }

        /// <summary></summary>
        [System.Obsolete("This method has been deprecated.")]
        protected void CopyLicense()
        {
        }

        /// <summary></summary>
        [System.Obsolete("This method has been deprecated.")]
        protected void CopyReadme()
        {
        }

        /// <summary></summary>
        [System.Obsolete("This method has been deprecated.")]
        protected void CopyErrorMessage()
        {
        }

        private static string GetFullDescription(System.Exception e)
        {
            return e.ToString();
        }

        private static string GetShortDescription(System.Exception e)
        {
            string message = e.Message;
            int num = message.IndexOf("(method:", System.StringComparison.InvariantCulture);
            if (num > -1)
            {
                return message.Substring(0, num - 1);
            }
            return message;
        }

        private static void SetVisibility(Control control, bool visible)
        {
            Context.ClientPage.ClientResponse.SetStyle(control.ID, "display", visible ? "" : "none");
        }

        private bool LoadPackage()
        {
            string text = this.PackageFile.Value;
            if (System.IO.Path.GetExtension(text).Trim().Length == 0)
            {
                text = System.IO.Path.ChangeExtension(text, ".zip");
                this.PackageFile.Value = text;
            }
            if (text.Trim().Length == 0)
            {
                Context.ClientPage.ClientResponse.Alert("Please specify a package.");
                return false;
            }
            text = Installer.GetFilename(text);
            if (!FileUtil.FileExists(text))
            {
                Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" file does not exist.", new object[]
                {
                    text
                }));
                return false;
            }
            IProcessingContext processingContext = Installer.CreatePreviewContext();
            ISource<PackageEntry> source = new PackageReader(MainUtil.MapPath(text));
            MetadataView metadataView = new MetadataView(processingContext);
            MetadataSink metadataSink = new MetadataSink(metadataView);
            metadataSink.Initialize(processingContext);
            source.Populate(metadataSink);
            if (processingContext == null || processingContext.Data == null)
            {
                Context.ClientPage.ClientResponse.Alert(Translate.Text("The package \"{0}\" could not be loaded.\n\nThe file maybe corrupt.", new object[]
                {
                    text
                }));
                return false;
            }
            this.PackageVersion = (processingContext.Data.ContainsKey("installer-version") ? 2 : 1);
            this.PackageName.Value = metadataView.PackageName;
            this.Version.Value = metadataView.Version;
            this.Author.Value = metadataView.Author;
            this.Publisher.Value = metadataView.Publisher;
            this.LicenseAgreement.InnerHtml = metadataView.License;
            this.ReadmeText.Value = metadataView.Readme;
            this.HasLicense = (metadataView.License.Length > 0);
            this.HasReadme = (metadataView.Readme.Length > 0);
            this.PostAction = metadataView.PostStep;
            Registry.SetString("Packager/File", this.PackageFile.Value);
            return true;
        }

        private void StartTask(string packageFile)
        {
            this.Monitor.Start("Install", "Install", new System.Threading.ThreadStart(new InstallPackageForm.AsyncHelper(packageFile).Install));
        }

        private void WatchForInstallationStatus()
        {
            string statusFileName = FileInstaller.GetStatusFileName(this.MainInstallationTaskID);
            this.Monitor.Start("WatchStatus", "Install", new System.Threading.ThreadStart(new InstallPackageForm.AsyncHelper().SetStatusFile(statusFileName).WatchForStatus));
        }

        private void StartInstallingSecurity()
        {
            string filename = Installer.GetFilename(this.PackageFile.Value);
            this.Monitor.Start("InstallSecurity", "Install", new System.Threading.ThreadStart(new InstallPackageForm.AsyncHelper(filename).InstallSecurity));
        }

        private void StartPostAction()
        {
            if (this.Monitor.JobHandle != Handle.Null)
            {
                Log.Info("Waiting for installation task completion", this);
                SheerResponse.Timer("installer:doPostAction", 100);
                return;
            }
            string text = this.PostAction;
            this.PostAction = string.Empty;
            if (text.IndexOf("://", System.StringComparison.InvariantCulture) < 0 && text.StartsWith("/", System.StringComparison.InvariantCulture))
            {
                text = WebUtil.GetServerUrl() + text;
            }
            this.Monitor.Start("RunPostAction", "Install", new System.Threading.ThreadStart(new InstallPackageForm.AsyncHelper(text, this.GetContextWithMetadata()).ExecutePostStep));
        }

        private IProcessingContext GetContextWithMetadata()
        {
            string filename = Installer.GetFilename(this.PackageFile.Value);
            IProcessingContext processingContext = Installer.CreatePreviewContext();
            ISource<PackageEntry> source = new PackageReader(MainUtil.MapPath(filename));
            MetadataView view = new MetadataView(processingContext);
            MetadataSink metadataSink = new MetadataSink(view);
            metadataSink.Initialize(processingContext);
            source.Populate(metadataSink);
            return processingContext;
        }
    }
}