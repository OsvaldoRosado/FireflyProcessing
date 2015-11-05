using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;

namespace FireflyProcessing.Controllers
{
    public class ConvertController : ApiController
    {
        private static readonly string AZURE_CONN_VAR = "AZURE_STORAGE_CONNECTION";
        private static readonly string AZURE_CONTAINER = "convert";

        public class RetData
        {
            public bool success;
            public string msg;
        }

        private bool ConvertFile(string source, string destDir)
        {
            // Fix unoconv weirdness with paths
            source = source.Replace("C:", "");
            destDir = destDir.Replace("C:", "");

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = String.Format("C:\\Unoconv\\unoconv.py -n -f html -e PublishMode=0 -o {0}\\start {1}", destDir, source),
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }

        private string UploadFolderToAzure(string dir)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                Environment.GetEnvironmentVariable(AZURE_CONN_VAR));

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(AZURE_CONTAINER);

            foreach (string file in Directory.GetFiles(dir))
            {
                var blobName = dir.Split('\\').Last() + "\\" + file.Split('\\').Last();
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
                blockBlob.UploadFromFile(file, FileMode.Open);
            }

            return container.Uri.AbsoluteUri + "/" + dir.Split('\\').Last() + "/";
        }

        public RetData Post()
        {
            var httpRequest = HttpContext.Current.Request;
            if (httpRequest.Files.Count == 1)
            {
                try
                {
                    var id = Guid.NewGuid().ToString();
                    var file = httpRequest.Files.Get(0);
                    var destPath = String.Format("{0}{1}-{2}", Path.GetTempPath(), id, file.FileName);
                    file.SaveAs(destPath);

                    // Perform conversion
                    var convertedDir = String.Format("{0}{1}", Path.GetTempPath(), id);
                    if (!ConvertFile(destPath,convertedDir))
                    {
                        // Conversion failed!
                        return new RetData { success = false, msg = "Conversion failed." };
                    }

                    // Upload to Azure Blob Service
                    UploadFolderToAzure(convertedDir);
                    var numSlides = (Directory.GetFiles(convertedDir).Length - 1) / 2;

                    var pubId = id + "-" + numSlides;

                    return new RetData { success = true, msg = pubId };
                }
                catch (Exception e)
                {
                    return new RetData { success = false, msg = e.ToString() };
                }
            }

            return new RetData { success = false, msg = "No file" };
        }
    }
}
