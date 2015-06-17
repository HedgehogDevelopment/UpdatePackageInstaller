using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NDesk.Options;
using System.IO;
using System.Reflection;
using System.Security.Permissions;
using System.ServiceModel;

namespace HedgehogDevelopment.PackageInstaller
{
    /// <summary>
    /// Installer command line utility. Uses NDesk.Options to parse the command line. For more information, please see
    /// http://www.ndesk.org/Options. 
    /// </summary>
    class Program
    {
        static int verbosity;
        static string SitecoreConnectorDLL { get; set; }
        static string SitecoreConnectorASMX { get; set; }

        static void Main(string[] args)
        {
            #region Declare options and installer variables
            // Installer variables
            string packagePath = null;
            string sitecoreWebURL = null;
            string sitecoreDeployFolder = null;
            bool show_help = args.Length == 0;

            // Options declaration
            OptionSet options = new OptionSet() {
   	            { "p|packagePath=", "The {PACKAGE PATH} is the path to the package. The package must be located in a folder reachable by the web server.\n",
                    v => packagePath = v },
                { "u|sitecoreUrl=", "The {SITECORE URL} is the url to the root of the Sitecore server.\n",
                    v => sitecoreWebURL = v },
                { "f|sitecoreDeployFolder=", "The {SITECORE DEPLOY FOLDER} is the UNC path to the Sitecore web root.\n",
                    v => sitecoreDeployFolder = v },
   	            { "v", "Increase debug message verbosity.\n",
                    v => { if (v != null) ++verbosity; } },
   	            { "h|help",  "Show this message and exit.", 
                    v => show_help = v != null },
             };
            #endregion

            // Parse options - exit on error
            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException e)
            {
                ShowError(e.Message);
                Environment.Exit(100);
            }

            // Display help if one is requested or no parameters are provided
            if (show_help)
            {
                ShowHelp(options);
                return;
            }

            #region Validate and process parameters
            bool parameterMissing = false;

            if (string.IsNullOrEmpty(packagePath))
            {
                ShowError("Package Path is required.");

                parameterMissing = true;
            }

            if (string.IsNullOrEmpty(sitecoreWebURL))
            {
                ShowError("Sitecore Web URL ie required.");

                parameterMissing = true;
            }

            if (string.IsNullOrEmpty(sitecoreDeployFolder))
            {
                ShowError("Sitecore Deploy folder is required.");

                parameterMissing = true;
            }

            if (!parameterMissing)
            {
                if (Directory.Exists(sitecoreDeployFolder))
                {
                    try
                    {
                        Debug("Initializing update package installation: {0}", packagePath);
                        if (sitecoreDeployFolder.LastIndexOf(@"\") != sitecoreDeployFolder.Length - 1)
                        {
                            sitecoreDeployFolder = sitecoreDeployFolder + @"\";
                        }

                        if (sitecoreWebURL.LastIndexOf(@"/") != sitecoreWebURL.Length - 1)
                        {
                            sitecoreWebURL = sitecoreWebURL + @"/";
                        }

                        // Install Sitecore connector
                        if (DeploySitecoreConnector(sitecoreDeployFolder))
                        {
                            using (TdsPackageInstaller.TdsPackageInstaller service = new TdsPackageInstaller.TdsPackageInstaller())
                            {
                                service.Url = string.Concat(sitecoreWebURL, Properties.Settings.Default.SitecoreConnectorFolder, "/TdsPackageInstaller.asmx");
                                service.Timeout = 600000;
 
                                Debug("Initializing package installation ..");

                                service.InstallPackage(packagePath);

                                Debug("Update package installed successfully.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Sitecore connector deployment failed.");

                            Environment.Exit(101);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception: {0}({1})\n{2}", ex.Message, ex.GetType().Name, ex.StackTrace);

                        if (ex.InnerException != null)
                        {
                            Console.WriteLine("\n\nInnerException: {0}({1})\n{2}", ex.InnerException.Message, ex.InnerException.GetType().Name, ex.InnerException.StackTrace);
                        }

                        Environment.Exit(102);
                    }
                    finally
                    {
                        // Remove Sitecore connection
                        RemoveSitecoreConnector();
                    }
                }
                else
                {
                    ShowError(string.Format("Sitecore Deploy Folder {0} not found.", sitecoreDeployFolder));
                }
            }

            #endregion
        }

        /// <summary>
        /// Displays the help message
        /// </summary>
        /// <param name="opts"></param>
        static void ShowHelp(OptionSet opts)
        {
            Console.WriteLine("Usage: packageinstaller [OPTIONS]");
            Console.WriteLine("Installs a sitecore package.");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine(@"-v -sitecoreUrl ""http://mysite.com/"" -sitecoreDeployFolder ""C:\inetpub\wwwroot\mysite\Website"" -packagePath ""C:\Package1.update""");
            Console.WriteLine();
            Console.WriteLine("Options:");

            opts.WriteOptionDescriptions(Console.Out);
        }

        /// <summary>
        /// Displays an error message
        /// </summary>
        /// <param name="message"></param>
        static void ShowError(string message)
        {
            Console.Write("Error: ");
            Console.WriteLine(message);
            Console.WriteLine("Try `packageinstaller --help' for more information.");
        }

        /// <summary>
        /// Deploys the 
        /// </summary>
        /// <param name="sitecoreDeployFolder"></param>
        /// <returns></returns>
        static bool DeploySitecoreConnector(string sitecoreDeployFolder)
        {
            Debug("Initializing Sitecore connector ...");

            string sourceFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            FileInfo serviceLibrary = new FileInfo(sourceFolder + @"\HedgehogDevelopment.TDS.PackageInstallerService.dll");
            FileInfo serviceFile = new FileInfo(sourceFolder + @"\Includes\TdsPackageInstaller.asmx");

            if (!serviceLibrary.Exists)
            {
                ShowError("Cannot find file " + serviceLibrary);

                return false;
            }

            if (!serviceFile.Exists)
            {
                ShowError("Cannot find file " + serviceFile);

                return false;
            }

            if (!Directory.Exists(sitecoreDeployFolder + Properties.Settings.Default.SitecoreConnectorFolder))
            {
                Directory.CreateDirectory(sitecoreDeployFolder + Properties.Settings.Default.SitecoreConnectorFolder);
            }

            SitecoreConnectorDLL = sitecoreDeployFolder + @"bin\" + serviceLibrary.Name;
            SitecoreConnectorASMX = sitecoreDeployFolder + Properties.Settings.Default.SitecoreConnectorFolder + @"\" + serviceFile.Name;

            if (File.Exists(SitecoreConnectorDLL))
            {
                File.SetAttributes(SitecoreConnectorDLL, FileAttributes.Normal);
                File.SetAttributes(SitecoreConnectorASMX, FileAttributes.Normal);
            }

            if (File.Exists(SitecoreConnectorASMX))
            {
                File.SetAttributes(SitecoreConnectorASMX, FileAttributes.Normal);
            }

            File.Copy(serviceLibrary.FullName, SitecoreConnectorDLL, true);
            File.Copy(serviceFile.FullName, SitecoreConnectorASMX, true);

            Debug("Sitecore connector deployed successfully.");

            return true;
        }

        /// <summary>
        /// Removes the sitecore connector from the site
        /// </summary>
        static void RemoveSitecoreConnector()
        {
            if (!string.IsNullOrEmpty(SitecoreConnectorDLL) && !string.IsNullOrEmpty(SitecoreConnectorASMX))
            {
                File.SetAttributes(SitecoreConnectorDLL, FileAttributes.Normal);
                File.SetAttributes(SitecoreConnectorASMX, FileAttributes.Normal);

                File.Delete(SitecoreConnectorDLL);
                File.Delete(SitecoreConnectorASMX);

                Debug("Sitecore connector removed successfully.");
            }
        }

        /// <summary>
        /// Writes a debug message to the console
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        static void Debug(string format, params object[] args)
        {
            if (verbosity > 0)
            {
                Console.Write(string.Format("[{0}] ", DateTime.Now.ToString("hh:mm:ss")));
                Console.WriteLine(format, args);
            }
        }
    }
}
