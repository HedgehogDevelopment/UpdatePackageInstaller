using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Services;
using System.ComponentModel;
using System.Web.Services.Protocols;
using System.Xml;
using Sitecore.Update.Installer;
using Sitecore.SecurityModel;
using log4net;
using log4net.Config;
using Sitecore.Update.Installer.Utils;
using Sitecore.Update;
using Sitecore.Update.Metadata;
using System.Configuration;
using Sitecore.Update.Utils;

namespace HedgehogDevelopment.TDS.PackageInstallerService
{
    [WebService(Namespace = "http://hhogdev.com/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    public class TdsPackageInstaller
    {
        /// <summary>
        /// Installs a Sitecore Update Package.
        /// </summary>
        /// <param name="path">A path to a package that is reachable by the web server</param>
        [WebMethod(Description = "Installs a Sitecore Update Package.")]
        public void InstallPackage(string path)
        {
            bool hasPostAction;
            string historyPath;

            // Use default logger
            ILog log = LogManager.GetLogger("root");
            XmlConfigurator.Configure((XmlElement)ConfigurationManager.GetSection("log4net"));

            using (new SecurityDisabler())
            {
                DiffInstaller installer = new DiffInstaller(UpgradeAction.Upgrade);
                MetadataView view = UpdateHelper.LoadMetadata(path);

                //Get the package entries
                List<ContingencyEntry> entries = installer.InstallPackage(path, InstallMode.Install, log, out hasPostAction, out historyPath);

                installer.ExecutePostInstallationInstructions(path, historyPath, InstallMode.Install, view, log, ref entries);

#if SITECORE_6X
                UpdateHelper.SaveInstallationMessages(entries, historyPath);
#endif
            }
        }
    }
}
