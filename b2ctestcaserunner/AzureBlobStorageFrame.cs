using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;

namespace Tools
{
    // https://docs.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata
    class AzureBlobStorageFrame
    {
        // Storage account / container / blob
        // Drive           / directory / file

        BlobServiceClient blobServiceClient = null;
        BlobContainerClient containerClient = null;
        BlobClient blobClient = null;
        object eTag = null;

        public string blobName = "";
        public string thrownException = "";

        public AzureBlobStorageFrame(string connectionString, string containerName = "")
        {
            // Create a BlobServiceClient object which will be used to create a container client
            blobServiceClient = new BlobServiceClient(connectionString);

            if (!string.IsNullOrEmpty(containerName))
                containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }


        /// <summary>
        /// create a container in blob storage
        /// </summary>
        /// <param name="containerName">Should be alphanumeric or dash, lower case</param>
        /// <returns>returns a container name, or "" on failure</returns>
        public string CreateContainer(string containerName)
        {
            string result = "";
            try
            {
                // Create the container and return a container client object
                containerClient = blobServiceClient.CreateBlobContainerAsync(containerName.ToLower()).Result;
                result = containerClient.Name;
            }
            catch { }
            return result;
        }


        /// <summary>
        /// Get list of blobs in container
        /// </summary>
        /// <param name="blobContainerName"></param>
        /// <returns>list of blobItems</returns>
        public async Task<List<BlobItem>> GetBlobInfo(string blobContainerName)
        {
            containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);

            List<BlobItem> blobData = new List<BlobItem>();

            // List all blobs in the container
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                blobData.Add(blobItem);
            }

            return blobData;
        }


        /// <summary>
        /// upload a file to blob storage
        /// </summary>
        /// <param name="blobFileName">dest: blob name in Azure</param>
        /// <param name="localFilePath">source: file name</param>
        /// <returns>"" on success, exception on error</returns>
        public string UploadFile(string containerName, string blobFileName, string localFilePath)
        {
            string response = "";
            
            try
            {
                var data = File.ReadAllText(localFilePath);
                response = UpsertBlob(containerName, blobFileName, data);
            }
            catch(Exception ex)
            {
                response = ex.ToString();
            }
            
            return response;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="containerName"></param>
        /// <param name="blobFileName"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public string UpsertBlob(string containerName, string blobFileName, string data)
        {
            string response = "";

            try
            {
                if (!string.IsNullOrEmpty(containerName))
                    containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                blobClient = containerClient.GetBlobClient(blobFileName);
                blobClient.DeleteIfExists();
                BlobContentInfo bci =
                    blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(data.ToString()))).Result;
            }
            catch (Exception ex)
            {
                response = ex.ToString();
            }

            return response;
        }


        /// <summary>
        /// download a file from blob storage
        /// </summary>
        /// <param name="blobFileName">source: blob name in Azure</param>
        /// <param name="downloadFilePath">dest: name of file</param>
        /// <returns>"" on success, exception on error</returns>
        public string DownloadFile(string blobFileName, string downloadFilePath)
        {
            string response = "";
            try
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobFileName);
                Azure.Response azureResponse = blobClient.DownloadToAsync(downloadFilePath).Result;
            }
            catch (Exception ex)
            {
                response = ex.ToString();
            }
            return response;
        }


        /// <summary>
        /// return the contents of a blob file as a string
        /// </summary>
        /// <param name="blobFileName"></param>
        /// <returns>file contents if successful, null otherwise</returns>
        public string ReadAllText(string blobContainerName, string blobFileName)
        {
            string responseText = null;

            try
            {
                containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobFileName.ToLower());
                var response = blobClient.DownloadAsync().Result;
                using (var streamReader = new StreamReader(response.Value.Content))
                {
                    responseText = streamReader.ReadToEndAsync().Result;
                }
            }
            catch (Exception ex)
            {

            }

            return responseText;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="containerName"></param>
        /// <returns></returns>
        public List<string> GetBlobNames(string containerName)
        {
            List<string> list = new List<string>();

            var blobItems = GetBlobInfo(containerName).Result;

            foreach(var item in blobItems)
            {
                list.Add(item.Name);
            }

            return list;
        }


        public List<string> GetContainerNames()
        {
            List<string> results = new List<string>();

            var containerList = blobServiceClient.GetBlobContainers();
            foreach (var c in containerList)
            {
                results.Add(c.Name);
            }

            return results;
        }


        public void UnitTests()
        {

        }
    }   // class
}   // namespace
