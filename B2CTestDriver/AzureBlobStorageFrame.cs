using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.IO;

namespace Tools
{
    class AzureBlobStorageFrame
    {
        // Storage account / container / blob
        // Drive           / directory / file

        BlobServiceClient blobServiceClient = null;
        BlobContainerClient containerClient = null;
        BlobClient blobClient = null;
        object eTag = null;

        public string blobName = "";


        public AzureBlobStorageFrame(string connectionString, string containerName)
        {
            // Create a BlobServiceClient object which will be used to create a container client
            blobServiceClient = new BlobServiceClient(connectionString);
            containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        }


        /// <summary>
        /// create a container in blob storage
        /// </summary>
        /// <param name="containerName"></param>
        public async void CreateContainerAsync(string containerName)
        {
            // Create the container and return a container client object
            containerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);
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
        public string UploadFile(string blobFileName, string localFilePath)
        {
            string response = "";
            try
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobFileName);
                BlobContentInfo bci = blobClient.UploadAsync(localFilePath, true).Result;
            }
            catch(Exception ex)
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
        public string ReadAllText(string blobFileName)
        {
            string responseText = null;

            try
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobFileName);
                var response = blobClient.DownloadAsync().Result;
                using (var streamReader = new StreamReader(response.Value.Content))
                {
                    responseText = streamReader.ReadToEndAsync().Result;
                }
            }
            catch(Exception ex)
            {

            }

            return responseText;
        }

    }   // class
}   // namespace
