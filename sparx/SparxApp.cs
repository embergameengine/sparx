using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace sparx
{
    public class SparxApp
    {
        string[] args;
        string emberPath => Environment.GetEnvironmentVariable("EmberPath");
        string sparxEmberVersionPath => Path.Join(emberPath, ".sparxversion");
        string emberVersion => File.ReadAllText(sparxEmberVersionPath);
        string emberVersionInstallPath => Path.Join(emberPath, "Versions", emberVersion);

        public void HelpCommand()
        {
            Console.WriteLine("\nhelp:");
            Console.WriteLine("\ninstall <creator>/<package> or install <path to .sprk file>: installs a spark to the global cache");
            Console.WriteLine("use <creator>/<package>: specifies your project to use the specified spark");
            Console.WriteLine("build: builds your project with references to the sparks you are using");

            Console.WriteLine("emberversion <ember version>: sets the version of ember to use");

        }

        public static float DirSize(DirectoryInfo d)
        {
            float size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
            return size / 1048576f;
        }

        public void InstallSparkFromPath(string fileName)
        {
            string installPath = Path.Join(emberVersionInstallPath, "SparxGlobalCache", Path.GetFileNameWithoutExtension(fileName));

            Console.WriteLine("installing spark...");

            Directory.CreateDirectory(installPath);

            ZipFile.ExtractToDirectory(fileName, installPath);

            Spark spark = JsonSerializer.Deserialize<Spark>(File.ReadAllText(Path.Join(installPath, ".spark")));

            Console.WriteLine($"are you sure you want to install spark {spark.name} version {spark.version}?");
            Console.WriteLine($"it will take up {DirSize(new DirectoryInfo(installPath)).ToString("n2")} mb of disk space");

            Console.Write("\ny/n? ");

            ConsoleKeyInfo info = Console.ReadKey();

            Console.Write("\n");

            if (info.KeyChar == 'y')
            {
                Console.WriteLine("installed spark!");
            }
            else
            {
                Console.WriteLine("aborting");

                Directory.Delete(installPath, true);
            }
        }

        public void InstallCommand()
        {
            string fileName = args[1];
            if (File.Exists(fileName))
            {
                InstallSparkFromPath(fileName);
            } else
            {
                string repoUrl = fileName;

                HttpClient client = new HttpClient();

                client.DefaultRequestHeaders.UserAgent.TryParseAdd("sparx/1");

                HttpResponseMessage response = client.GetAsync($"https://api.github.com/repos/{repoUrl}/releases").GetAwaiter().GetResult();
            
                if (response.IsSuccessStatusCode)
                {
                    string responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                
                    List<GithubRelease> releases = JsonSerializer.Deserialize<List<GithubRelease>>(responseJson);

                    foreach (GithubRelease release in releases)
                    {
                        if (release.tag_name == args[2])
                        {
                            foreach (GithubAsset asset in release.assets)
                            {
                                if (new Uri(asset.browser_download_url).Segments.Last().EndsWith(".spkg"))
                                {
                                    Console.WriteLine("found release! installing..");

                                    string installPath = Path.Join(emberVersionInstallPath, "SparxGlobalCache", repoUrl.Split("/")[1]);

                                    Console.WriteLine("downloading...");

                                    using (HttpClient downloadClient = new HttpClient())
                                    {
                                        using (var s = downloadClient.GetStreamAsync(asset.browser_download_url))
                                        {
                                            using (var fs = new FileStream(Path.Join(Environment.GetEnvironmentVariable("Temp"), repoUrl.Replace("/", " ") + ".spkg"), FileMode.OpenOrCreate))
                                            {
                                                s.Result.CopyTo(fs);
                                            }
                                        }
                                    }

                                    Console.WriteLine("installing...");

                                    InstallSparkFromPath(Path.Join(Environment.GetEnvironmentVariable("Temp"), repoUrl.Replace("/", " ") + ".spkg"));

                                    File.Delete(Path.Join(Environment.GetEnvironmentVariable("Temp"), repoUrl.Replace("/", " ") + ".spkg"));

                                }


                                
                            }

                            break;
                        }
                    }
                } else
                {
                    Console.WriteLine("failed to connect to github api. check your internet connection and try again");
                }
            }
            
        }

        public void UseCommand()
        {
            Console.WriteLine("hi");
            string sparkConfigJson = File.ReadAllText(".spark");

            SparxConfig config = JsonSerializer.Deserialize<SparxConfig>(sparkConfigJson);

            string sparkToAdd = args[1].Replace("/", " ");
            
            string sparkPath = Path.Join(emberVersionInstallPath, "SparxGlobalCache", sparkToAdd);

            if (Directory.Exists(sparkPath))
            {
                Console.WriteLine("found spark in global cache! adding to project...");

                config.sparks.Add(sparkToAdd.Replace(" ", "/"));
            }

            File.WriteAllText(".spark", JsonSerializer.Serialize(config));
        }

        private void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public void BuildCommand()
        {
            if (File.Exists("SparxProject.csproj"))
            {
                File.Delete("SparxProject.csproj");
            }

            string sparxConfigJson = File.ReadAllText(".spark");

            SparxConfig config = JsonSerializer.Deserialize<SparxConfig>(sparxConfigJson);

            XmlDocument project = new XmlDocument();

            XmlElement rootElement = project.CreateElement("Project");

            XmlAttribute sdkAttribute = project.CreateAttribute("Sdk");
            sdkAttribute.Value = "Microsoft.NET.Sdk";

            rootElement.Attributes.Append(sdkAttribute);

            XmlElement propertyGroupElement = project.CreateElement("PropertyGroup");
            XmlElement targetFrameworkElement = project.CreateElement("TargetFramework");
            XmlElement implicitUsingsElement = project.CreateElement("ImplicitUsings");
            XmlElement nullableElement = project.CreateElement("Nullable");

            targetFrameworkElement.InnerText = "net6.0";
            implicitUsingsElement.InnerText = "enable";
            nullableElement.InnerText = "enable";

            propertyGroupElement.AppendChild(targetFrameworkElement);
            propertyGroupElement.AppendChild(implicitUsingsElement);
            propertyGroupElement.AppendChild(nullableElement);

            rootElement.AppendChild(propertyGroupElement);

            XmlElement itemGroup = project.CreateElement("ItemGroup");

            foreach (string file in Directory.EnumerateFiles("lib"))
            {
                XmlElement reference = project.CreateElement("Reference");

                XmlAttribute includeAttribute = project.CreateAttribute("Include");
                includeAttribute.Value = Path.GetFileName(file).Replace(".dll", "");

                reference.Attributes.Append(includeAttribute);

                XmlElement hintPath = project.CreateElement("HintPath");

                hintPath.InnerText = file;

                reference.AppendChild(hintPath);

                itemGroup.AppendChild(reference);
            }

            foreach (string spark in config.sparks)
            {
                string sparkPath = Path.Join(emberVersionInstallPath, "SparxGlobalCache", spark.Replace("/", " "));

                Spark sparkPackageConfig = JsonSerializer.Deserialize<Spark>(File.ReadAllText(Path.Join(sparkPath, ".spark")));

                XmlElement reference = project.CreateElement("Reference");

                XmlAttribute includeAttribute = project.CreateAttribute("Include");
                includeAttribute.Value = Path.GetFileName(Path.Join(sparkPath, sparkPackageConfig.libraryDll)).Replace(".dll", "");

                reference.Attributes.Append(includeAttribute);

                XmlElement hintPath = project.CreateElement("HintPath");

                hintPath.InnerText = Path.Join(sparkPath, sparkPackageConfig.libraryDll);

                reference.AppendChild(hintPath);

                itemGroup.AppendChild(reference);
            }

            rootElement.AppendChild(itemGroup);

            project.AppendChild(rootElement);

            project.Save(config.name + ".csproj");

            Console.WriteLine("created project files. building...");

            File.Move(config.name + ".csproj", "SparxProject.csproj");

            string exeName = "dotnet.exe";
            string pathVar = Environment.GetEnvironmentVariable("PATH");
            string[] paths = pathVar.Split(Path.PathSeparator);
            Process p = null;
            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, exeName);
                if (File.Exists(fullPath))
                {
                    ProcessStartInfo info = new ProcessStartInfo();

                    info.Arguments = "build SparxProject.csproj";
                    info.FileName = fullPath;
                    info.WorkingDirectory = Environment.CurrentDirectory;

                    p = Process.Start(info);
                    break;
                }
            }

            if (p == null)
            {
                Console.WriteLine("couldn't find dotnet executable on the path. did you install dotnet?");
                return;
            }

            p.WaitForExit();

            Console.WriteLine("build complete!");

            if (Directory.Exists("..\\Assets"))
            {
                Console.WriteLine("copying assets...");

                CopyFilesRecursively("..\\Assets", "bin\\Debug\\net6.0\\");
            }
        }

        public void CreateCommand()
        {
            
        }

        public void VersionSetCommand()
        {
            if (Directory.Exists(Path.Join(emberPath, "Versions", args[1])))
            {
                Console.WriteLine("found ember version. setting as default...");
                if (File.Exists(sparxEmberVersionPath))
                {
                    File.WriteAllText(sparxEmberVersionPath, args[1]);
                } else
                {
                    File.Create(sparxEmberVersionPath);
                    File.WriteAllText(sparxEmberVersionPath, args[1]);
                }
            } else
            {
                Console.WriteLine("couldnt find that version of ember. are you sure you installed it?");
            }
        }

        public void Start(string[] args)
        {
            this.args = args;
            switch (args[0])
            {
                case "help":
                    HelpCommand();
                    break;

                case "add":
                    InstallCommand();
                    break;

                case "use":
                    UseCommand();
                    break;

                case "build":
                    BuildCommand();
                    break;

                case "emberversion":
                    VersionSetCommand();
                    break;
            }
        }
    }
}
