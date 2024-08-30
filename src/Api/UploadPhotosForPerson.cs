using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Azure.Storage.Blobs;
using BlazorApp.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace Api
{
   

    public class UploadPhotosForPerson
    {
        private readonly ILogger _logger;
        private readonly IConfiguration configuration;

        public UploadPhotosForPerson(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<UploadPhotosForPerson>();
            this.configuration = configuration;
        }

        [Function("UploadPhotosForPerson")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {

                var content = await new StreamReader(req.Body).ReadToEndAsync();

                UploadRequest? uploadRequest = JsonConvert.DeserializeObject<UploadRequest>(content);

                if (uploadRequest == null)
                {
                    _logger.LogWarning("Could not parse request: " + content);
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }


                if (string.IsNullOrEmpty(uploadRequest.PersonName) || uploadRequest.Photos.Count() == 0)
                {
                    _logger.LogWarning($"Empty person name or no photos provided.");
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
                _logger.LogInformation($"Uploading {uploadRequest.Photos.Count()} to storage for {uploadRequest.PersonName}");

                var storageName = uploadRequest.PersonName.ToLower();
                if (!IsValidStorageContainerName(storageName))
                {
                    storageName = Guid.NewGuid().ToString();
                }

                var connection = System.Environment.GetEnvironmentVariable("StorageAccountConnectionString");
                var blobServiceClient = new BlobServiceClient(connection);

                if(!CreateIfNotContainerNameExistsAlready(blobServiceClient, storageName))
                {
                    _logger.LogError($"Failed to create storage container {storageName}");
                    return req.CreateResponse(HttpStatusCode.InternalServerError);
                }

                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(storageName);
                foreach (var photo in uploadRequest.Photos)
                {
                    try
                    {
                        BlobClient blobClient = containerClient.GetBlobClient(photo.Key);

                        Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);
                        using (var stream = new MemoryStream(photo.Value, writable: false))
                        {
                            await blobClient.UploadAsync(stream);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to upload file. {ex.Message}", ex);
                    }
                }
                


                var response = req.CreateResponse(HttpStatusCode.OK);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
            
        }  
        private bool CreateIfNotContainerNameExistsAlready(BlobServiceClient blobServiceClient, string containerName)
        {
            var container = blobServiceClient.GetBlobContainerClient(containerName);

            //or you can directly use this method to create a container if it does not exist.
            container.CreateIfNotExists();

            return true;
        }
        private bool IsValidStorageContainerName(string containerName)
        {
            if (string.IsNullOrEmpty(containerName))
                return false;

            if(containerName.Length < 3)
                return false;

            if (containerName.Length > 24)
                return false;

            return true;
        }
    }
}
